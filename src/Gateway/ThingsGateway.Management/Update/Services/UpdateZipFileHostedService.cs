//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Reflection;
using System.Runtime.InteropServices;

using ThingsGateway.NewLife;
using ThingsGateway.NewLife.Log;
using ThingsGateway.Upgrade;

using TouchSocket.Core;
using TouchSocket.Dmtp;
using TouchSocket.Dmtp.FileTransfer;
using TouchSocket.Dmtp.Rpc;
using TouchSocket.Rpc;
using TouchSocket.Sockets;

namespace ThingsGateway.Management;


internal sealed class UpdateZipFileHostedService : BackgroundService, IUpdateZipFileHostedService
{
    public UpdateZipFileHostedService(ILogger<UpdateZipFileHostedService> logger, INoticeService noticeService, IVerificatInfoService verificatInfoService)
    {
        NoticeService = noticeService;
        VerificatInfoService = verificatInfoService;

        _logger = logger;
        Localizer = App.CreateLocalizerByType(typeof(UpdateZipFileHostedService));

        // 创建新的文件日志记录器，并设置日志级别为 Trace
        LogPath = "Logs/UpgradeLog";
        TextLogger = TextFileLogger.GetMultipleFileLogger(LogPath);
        TextLogger.LogLevel = TouchSocket.Core.LogLevel.Trace;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        TcpDmtpClient = await GetTcpDmtpClient().ConfigureAwait(false);
        var upgradeServerOptions = App.GetOptions<UpgradeServerOptions>();
        bool success = false;
        bool first = true;
        while (!success)
        {
            try
            {
                if (upgradeServerOptions.Enable)
                {
                    await TcpDmtpClient.ConnectAsync().ConfigureAwait(false);
                    await TcpDmtpClient.ResetIdAsync(upgradeServerOptions.Id).ConfigureAwait(false);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                if (first)
                    XTrace.WriteException(ex);
                first = false;
            }
            finally
            {
                await Task.Delay(3000, stoppingToken).ConfigureAwait(false);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (upgradeServerOptions.Enable && !App.HostEnvironment.IsDevelopment())
                {
                    var data = await GetList().ConfigureAwait(false);
                    if (data.Count != 0 && (LastVersion == null || data.FirstOrDefault().Version > LastVersion))
                    {
                        LastVersion = data.FirstOrDefault().Version;
                        var verificatInfoIds = VerificatInfoService.GetListByUserId(RoleConst.SuperAdminId);
                        await NoticeService.NavigationMesage(verificatInfoIds.Where(a => a.Online).SelectMany(a => a.ClientIds), "/gateway/system?tab=1", App.CreateLocalizerByType(typeof(UpdateZipFileHostedService))["Update"]).ConfigureAwait(false);
                    }
                }
                error = false;
                await Task.Delay(60000, stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!error)
                    TextLogger.LogWarning(ex);
                error = true;
            }
        }
    }



    private INoticeService NoticeService { get; set; }
    private IVerificatInfoService VerificatInfoService { get; set; }

    private Version LastVersion;
    private bool error;

    private ILogger<UpdateZipFileHostedService> _logger;
    private IStringLocalizer Localizer;
    private LoggerGroup _log { get; set; }
    public TextFileLogger TextLogger { get; }
    public string LogPath { get; }


    /// <summary>
    /// 传输限速
    /// </summary>
    public const long MaxSpeed = 1024 * 1024 * 10L;

    public TcpDmtpClient? TcpDmtpClient { get; set; }
    public async Task<List<UpdateZipFile>> GetList()
    {
        var upgradeServerOptions = App.GetOptions<UpgradeServerOptions>();
        if (!upgradeServerOptions.Enable)
            throw new Exception("未启用更新服务");

        //设置调用配置
        var tokenSource = new CancellationTokenSource();//可取消令箭源，可用于取消Rpc的调用
        var invokeOption = new DmtpInvokeOption()//调用配置
        {
            FeedbackType = FeedbackType.WaitInvoke,//调用反馈类型
            SerializationType = SerializationType.Json,//序列化类型
            Timeout = 5000,//调用超时设置
            Token = tokenSource.Token//配置可取消令箭
        };

        var updateZipFiles = await TcpDmtpClient.GetDmtpRpcActor().InvokeTAsync<List<UpdateZipFile>>(nameof(GetList), invokeOption, new UpdateZipFileInput()
        {
            Version = Assembly.GetEntryAssembly().GetName().Version,
            DotNetVersion = Environment.Version,
            OSPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                           RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" :
                           RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "OSX" : "Unknown",
            Architecture = RuntimeInformation.ProcessArchitecture,
            AppName = "ThingsGateway"
        }).ConfigureAwait(false);

        return updateZipFiles.OrderByDescending(a => a.Version).ToList();
    }

    private readonly WaitLock WaitLock = new();
    public async Task Update(UpdateZipFile updateZipFile, Func<Task<bool>> check = null)
    {
        try
        {
            var upgradeServerOptions = App.GetOptions<UpgradeServerOptions>();
            if (!upgradeServerOptions.Enable)
                return;
            if (WaitLock.Waited)
            {
                _log.LogWarning("正在更新中，请稍后再试");
                return;
            }
            try
            {

                await WaitLock.WaitAsync().ConfigureAwait(false);
                RestartServerHelper.DeleteAndBackup();

                var result = await ClientPullFileFromService(TcpDmtpClient, updateZipFile.FilePath).ConfigureAwait(false);
                if (result)
                {
                    if (check != null)
                        result = await check.Invoke().ConfigureAwait(false);
                    if (result)
                    {
                        RestartServerHelper.ExtractUpdate();
                    }
                }
                else
                {
                }
            }
            finally
            {
                WaitLock.Release();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex);

        }
    }

    /// <summary>
    /// 客户端从服务器下载文件。
    /// </summary>
    private static async Task<bool> ClientPullFileFromService(TcpDmtpClient client, string path)
    {
        var metadata = new Metadata();//传递到服务器的元数据
        metadata.Add(FileConst.FilePathKey, path);
        var fileOperator = new FileOperator//实例化本次传输的控制器，用于获取传输进度、速度、状态等。
        {
            SavePath = FileConst.UpgradePath,//客户端本地保存路径
            ResourcePath = path,//请求文件的资源路径
            Metadata = metadata,//传递到服务器的元数据
            Timeout = TimeSpan.FromSeconds(60),//传输超时时长
            TryCount = 10,//当遇到失败时，尝试次数
            FileSectionSize = 1024 * 512//分包大小，当网络较差时，应该适当减小该值
        };

        fileOperator.MaxSpeed = MaxSpeed;//设置最大限速。

        //此处的作用相当于Timer，定时每秒输出当前的传输进度和速度。
        var loopAction = LoopAction.CreateLoopAction(-1, 1000, (loop) =>
        {
            if (fileOperator.IsEnd)
            {
                loop.Dispose();
            }
            client.Logger.Info($"请求文件：{fileOperator.ResourcePath}，进度：{(fileOperator.Progress * 100).ToString("F2")}%，速度：{(fileOperator.Speed() / 1024).ToString("F2")} KB/s");
        });

        _ = loopAction.RunAsync();

        //此方法会阻塞，直到传输结束，也可以使用PullFileAsync
        var result = await client.GetDmtpFileTransferActor().PullFileAsync(fileOperator).ConfigureAwait(false);

        if (result.IsSuccess)
            client.Logger.Info(result.ToString());
        else
            client.Logger.Warning(result.ToString());

        return result.IsSuccess;
    }

    /// <summary>
    /// 客户端上传文件到服务器。
    /// </summary>
    private static async Task ClientPushFileFromService(TcpDmtpClient client, string serverPath, string resourcePath)
    {
        var metadata = new Metadata();//传递到服务器的元数据
        metadata.Add(FileConst.FilePathKey, serverPath);

        var fileOperator = new FileOperator//实例化本次传输的控制器，用于获取传输进度、速度、状态等。
        {
            SavePath = serverPath,//服务器本地保存路径
            ResourcePath = resourcePath,//客户端本地即将上传文件的资源路径
            Metadata = metadata,//传递到服务器的元数据
            Timeout = TimeSpan.FromSeconds(60),//传输超时时长
            TryCount = 10,//当遇到失败时，尝试次数
            FileSectionSize = 1024 * 512//分包大小，当网络较差时，应该适当减小该值
        };

        fileOperator.MaxSpeed = MaxSpeed;//设置最大限速。

        //此处的作用相当于Timer，定时每秒输出当前的传输进度和速度。
        var loopAction = LoopAction.CreateLoopAction(-1, 1000, (loop) =>
        {
            if (fileOperator.IsEnd)
            {
                loop.Dispose();
            }
            client.Logger.Info($"进度：{(fileOperator.Progress * 100).ToString("F2")}%，速度：{(fileOperator.Speed() / 1024).ToString("F2")} KB/s");
        });

        _ = loopAction.RunAsync();

        //此方法会阻塞，直到传输结束，也可以使用PushFileAsync
        var result = await client.GetDmtpFileTransferActor().PushFileAsync(fileOperator).ConfigureAwait(false);

        client.Logger.Info(result.ToString());

    }

    /// <summary>
    /// 底层错误日志输出
    /// </summary>
    internal void Log_Out(TouchSocket.Core.LogLevel arg1, object arg2, string arg3, Exception arg4)
    {
        _logger?.Log_Out(arg1, arg2, arg3, arg4);
    }

    private async Task<TcpDmtpClient> GetTcpDmtpClient()
    {
        _log = new LoggerGroup() { LogLevel = TouchSocket.Core.LogLevel.Trace };
        _log.AddLogger(new EasyLogger(Log_Out) { LogLevel = TouchSocket.Core.LogLevel.Trace });
        _log.AddLogger(TextLogger);
        var upgradeServerOptions = App.GetOptions<UpgradeServerOptions>();
        var config = new TouchSocketConfig()
               .SetRemoteIPHost(new IPHost($"{upgradeServerOptions.UpgradeServerIP}:{upgradeServerOptions.UpgradeServerPort}"))
               .SetDmtpOption(new DmtpOption()
               {
                   VerifyToken = upgradeServerOptions.VerifyToken
               })
               .ConfigureContainer(a =>
               {
                   a.AddRpcStore(store =>
                   {
                       store.RegisterServer<UpgradeRpcServer>();
                   });
                   a.AddLogger(_log);
                   a.AddDmtpRouteService();//添加路由策略
               })
               .ConfigurePlugins(a =>
               {
                   a.UseDmtpRpc();
                   a.UseDmtpFileTransfer();//必须添加文件传输插件

                   a.Add<FilePlugin>();

                   //使用重连
                   a.UseDmtpReconnection<TcpDmtpClient>()
                   .UsePolling(TimeSpan.FromSeconds(5))//使用轮询，每3秒检测一次
                   .SetActionForCheck(async (c, i) =>//重新定义检活策略
                   {
                       //方法1，直接判断是否在握手状态。使用该方式，最好和心跳插件配合使用。因为如果直接断网，则检测不出来
                       //await Task.CompletedTask;//消除Task
                       //return c.Online;//判断是否在握手状态

                       //方法2，直接ping，如果true，则客户端必在线。如果false，则客户端不一定不在线，原因是可能当前传输正在忙
                       if (await c.PingAsync().ConfigureAwait(false))
                       {
                           return true;
                       }
                       //返回false时可以判断，如果最近活动时间不超过3秒，则猜测客户端确实在忙，所以跳过本次重连
                       else if (DateTime.Now - c.GetLastActiveTime() < TimeSpan.FromSeconds(3))
                       {
                           return null;
                       }
                       //否则，直接重连。
                       else
                       {
                           return false;
                       }
                   });

                   a.UseDmtpHeartbeat()//使用Dmtp心跳
                   .SetTick(TimeSpan.FromSeconds(5))
                   .SetMaxFailCount(3);
               });
        TcpDmtpClient client = new();
        await client.SetupAsync(config).ConfigureAwait(false);

        return client;
    }

    public override void Dispose()
    {
        base.Dispose();
        TextLogger.Dispose();
    }
}
