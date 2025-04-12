//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using System.Collections.Concurrent;

using ThingsGateway.NewLife.Threading;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

/// <summary>
/// <para></para>
/// 采集插件，继承实现不同PLC通讯
/// <para></para>
/// </summary>
public abstract class CollectFoundationBase : CollectBase
{
    /// <summary>
    /// 底层驱动，有可能为null
    /// </summary>
    public virtual IDevice? FoundationDevice { get; }

    /// <summary>
    /// 是否连接成功
    /// </summary>
    public override bool IsConnected()
    {
        return FoundationDevice?.OnLine == true;
    }

    public override string ToString()
    {
        return FoundationDevice?.ToString() ?? base.ToString();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        FoundationDevice?.Dispose();
        base.Dispose(disposing);
    }
    /// <summary>
    /// 开始通讯执行的方法
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override async Task ProtectedStartAsync(CancellationToken cancellationToken)
    {
        if (FoundationDevice?.Channel != null)
            await FoundationDevice.Channel.ConnectAsync(FoundationDevice.Channel.ChannelOptions.ConnectTimeout, cancellationToken).ConfigureAwait(false);
    }

    public override string GetAddressDescription()
    {
        return FoundationDevice?.GetAddressDescription();
    }


    protected override async ValueTask<bool> TestOnline(CancellationToken cancellationToken)
    {
        //设备无法连接时
        // 检查协议是否为空，如果为空则抛出异常
        if (FoundationDevice != null)
        {
            if (FoundationDevice.OnLine == false)
            {
                Exception exception = null;
                try
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await FoundationDevice.Channel.ConnectAsync(FoundationDevice.Channel.ChannelOptions.ConnectTimeout, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    return true;
                }
                if (FoundationDevice.OnLine == false && exception != null)
                {
                    foreach (var item in CurrentDevice.VariableSourceReads)
                    {
                        if (item.LastErrorMessage != exception.Message)
                        {
                            if (!cancellationToken.IsCancellationRequested)
                                LogMessage?.LogWarning(exception, Localizer["CollectFail", DeviceName, item?.RegisterAddress, item?.Length, exception.Message]);
                        }
                        item.LastErrorMessage = exception.Message;
                        CurrentDevice.SetDeviceStatus(TimerX.Now, true, exception.Message);
                        var time = DateTime.Now;
                        item.VariableRuntimes.ForEach(a => a.SetValue(null, time, isOnline: false));
                    }
                    foreach (var item in CurrentDevice.ReadVariableMethods)
                    {
                        if (item.LastErrorMessage != exception.Message)
                        {
                            if (!cancellationToken.IsCancellationRequested)
                                LogMessage?.LogWarning(exception, Localizer["MethodFail", DeviceName, item.MethodInfo.Name, exception.Message]);
                        }
                        item.LastErrorMessage = exception.Message;
                        CurrentDevice.SetDeviceStatus(TimerX.Now, true, exception.Message);
                        var time = DateTime.Now;
                        item.Variable.SetValue(null, time, isOnline: false);
                    }

                    await Task.Delay(3000, cancellationToken).ConfigureAwait(false);
                    return true;
                }
            }
        }

        return false;
    }


    /// <summary>
    /// 采集驱动读取，读取成功后直接赋值变量，失败不做处理，注意非通用设备需重写
    /// </summary>
    protected override async ValueTask<OperResult<byte[]>> ReadSourceAsync(VariableSourceRead variableSourceRead, CancellationToken cancellationToken)
    {
        try
        {
            await ReadWriteLock.ReaderLockAsync(cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return new(new OperationCanceledException());
            // 从协议读取数据
            var read = await FoundationDevice.ReadAsync(variableSourceRead.RegisterAddress, variableSourceRead.Length, cancellationToken).ConfigureAwait(false);

            // 增加变量源的读取次数
            Interlocked.Increment(ref variableSourceRead.ReadCount);

            // 如果读取成功且有有效内容，则解析结构化内容
            if (read.IsSuccess)
            {
                var prase = variableSourceRead.VariableRuntimes.PraseStructContent(FoundationDevice, read.Content, false);
                return new OperResult<byte[]>(prase);
            }

            // 返回读取结果
            return read;
        }
        finally
        {
        }
    }

    /// <summary>
    /// 批量写入变量值,需返回变量名称/结果，注意非通用设备需重写
    /// </summary>
    /// <returns></returns>
    protected override async ValueTask<Dictionary<string, OperResult>> WriteValuesAsync(Dictionary<VariableRuntime, JToken> writeInfoLists, CancellationToken cancellationToken)
    {
        using var writeLock = ReadWriteLock.WriterLock();
        try
        {
            // 检查协议是否为空，如果为空则抛出异常
            if (FoundationDevice == null)
                throw new NotSupportedException();

            // 创建用于存储操作结果的并发字典
            ConcurrentDictionary<string, OperResult> operResults = new();

            // 使用并发方式遍历写入信息列表，并进行异步写入操作
            await writeInfoLists.ParallelForEachAsync(async (writeInfo, cancellationToken) =>
            {
                try
                {
                    // 调用协议的写入方法，将写入信息中的数据写入到对应的寄存器地址，并获取操作结果
                    var result = await FoundationDevice.WriteAsync(writeInfo.Key.RegisterAddress, writeInfo.Value, writeInfo.Key.DataType, cancellationToken).ConfigureAwait(false);

                    // 将操作结果添加到结果字典中，使用变量名称作为键
                    operResults.TryAdd(writeInfo.Key.Name, result);
                }
                catch (Exception ex)
                {
                    operResults.TryAdd(writeInfo.Key.Name, new(ex));
                }
            }, CollectProperties.MaxConcurrentCount, cancellationToken).ConfigureAwait(false);

            // 返回包含操作结果的字典
            return new Dictionary<string, OperResult>(operResults);
        }
        finally
        {
        }
    }

    private sealed class ReadResultCount
    {
        public int deviceMethodsVariableFailedNum = 0;
        public int deviceMethodsVariableSuccessNum = 0;
        public int deviceSourceVariableFailedNum = 0;
        public int deviceSourceVariableSuccessNum = 0;
    }

}
