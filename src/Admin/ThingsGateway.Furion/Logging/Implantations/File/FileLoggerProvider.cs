﻿// ------------------------------------------------------------------------
// 版权信息
// 版权归百小僧及百签科技（广东）有限公司所有。
// 所有权利保留。
// 官方网站：https://baiqian.com
//
// 许可证信息
// 项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。
// 许可证的完整文本可以在源代码树根目录中的 LICENSE-APACHE 和 LICENSE-MIT 文件中找到。
// ------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;

using ThingsGateway.Extension.Generic;

namespace ThingsGateway.Logging;

/// <summary>
/// 文件日志记录器提供程序
/// </summary>
/// <remarks>https://docs.microsoft.com/zh-cn/dotnet/core/extensions/custom-logging-provider</remarks>
[SuppressSniffer, ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    /// <summary>
    /// 存储多日志分类日志记录器
    /// </summary>
    private readonly ConcurrentDictionary<string, FileLogger> _fileLoggers = new();

    /// <summary>
    /// 日志消息队列（线程安全）
    /// </summary>
    private readonly BlockingCollection<LogMessage> _logMessageQueue = new(12000);

    /// <summary>
    /// 日志作用域提供器
    /// </summary>
    private IExternalScopeProvider _scopeProvider;

    /// <summary>
    /// 记录日志所有滚动文件名
    /// </summary>
    /// <remarks>只有 MaxRollingFiles 和 FileSizeLimitBytes 大于 0 有效</remarks>
    internal readonly ConcurrentDictionary<string, FileInfo> _rollingFileNames = new();

    /// <summary>
    /// 文件日志写入器
    /// </summary>
    private readonly FileLoggingWriter _fileLoggingWriter;

    /// <summary>
    /// 长时间运行的后台任务
    /// </summary>
    /// <remarks>实现不间断写入</remarks>
    private readonly Task _processQueueTask;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="fileName">日志文件名</param>
    public FileLoggerProvider(string fileName)
        : this(fileName, true)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="fileName">日志文件名</param>
    /// <param name="append">追加到已存在日志文件或覆盖它们</param>
    public FileLoggerProvider(string fileName, bool append)
        : this(fileName, new FileLoggerOptions() { Append = append })
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="fileName">日志文件名</param>
    /// <param name="fileLoggerOptions">文件日志记录器配置选项</param>
    public FileLoggerProvider(string fileName, FileLoggerOptions fileLoggerOptions)
    {
        // 支持文件名嵌入系统环境变量，格式为：%SystemDrive%，%SystemRoot%，处理 Windows 和 Linux 路径分隔符不一致问题
        FileName = Environment.ExpandEnvironmentVariables(fileName).Replace('\\', '/');
        LoggerOptions = fileLoggerOptions;

        // 创建文件日志写入器
        _fileLoggingWriter = new FileLoggingWriter(this);

        // 创建长时间运行的后台任务，并将日志消息队列中数据写入文件中
        _processQueueTask = Task.Factory.StartNew(async state => await ((FileLoggerProvider)state).ProcessQueueAsync().ConfigureAwait(false)
            , this, TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// 文件名
    /// </summary>
    internal string FileName;

    /// <summary>
    /// 文件日志记录器配置选项
    /// </summary>
    internal FileLoggerOptions LoggerOptions { get; private set; }

    /// <summary>
    /// 日志作用域提供器
    /// </summary>
    internal IExternalScopeProvider ScopeProvider => _scopeProvider;

    /// <summary>
    /// 创建文件日志记录器
    /// </summary>
    /// <param name="categoryName">日志分类名</param>
    /// <returns><see cref="ILogger"/></returns>
    public ILogger CreateLogger(string categoryName)
    {
        return _fileLoggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }
    public void RemoveCache(string categoryName)
    {
        _fileLoggers.Remove(categoryName);
    }

    /// <summary>
    /// 设置作用域提供器
    /// </summary>
    /// <param name="scopeProvider"></param>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// 释放非托管资源
    /// </summary>
    /// <remarks>控制日志消息队列</remarks>
    public void Dispose()
    {
        // 标记日志消息队列停止写入
        _logMessageQueue.CompleteAdding();

        try
        {
            // 设置 1.5秒的缓冲时间，避免还有日志消息没有完成写入文件中
            _processQueueTask?.Wait(1500);
        }
        catch (TaskCanceledException) { }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException) { }
        catch { }

        // 清空文件日志记录器
        _fileLoggers.Clear();

        // 清空滚动文件名记录器
        _rollingFileNames.Clear();

        // 释放内部文件写入器
        Task.Run(_fileLoggingWriter.CloseAsync);
    }

    /// <summary>
    /// 将日志消息写入队列中等待后台任务出队写入文件
    /// </summary>
    /// <param name="logMsg">日志消息</param>
    internal void WriteToQueue(LogMessage logMsg)
    {
        // 只有队列可持续入队才写入
        if (!_logMessageQueue.IsAddingCompleted)
        {
            try
            {
                _logMessageQueue.Add(logMsg);
                return;
            }
            catch (InvalidOperationException) { }
            catch { }
        }
    }

    /// <summary>
    /// 将日志消息写入文件中
    /// </summary>
    /// <returns></returns>
    private async Task ProcessQueueAsync()
    {
        foreach (var logMsg in _logMessageQueue.GetConsumingEnumerable())
        {
            await _fileLoggingWriter.WriteAsync(logMsg, _logMessageQueue.Count == 0).ConfigureAwait(false);
        }
    }
}