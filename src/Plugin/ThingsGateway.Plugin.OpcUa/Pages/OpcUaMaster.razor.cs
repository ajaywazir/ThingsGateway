﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------
#pragma warning disable CA2007 // 考虑对等待的任务调用 ConfigureAwait

using BootstrapBlazor.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using ThingsGateway.Foundation;
using ThingsGateway.Foundation.OpcUa;
using ThingsGateway.NewLife.Extension;
using ThingsGateway.NewLife.Json.Extension;
using ThingsGateway.Razor;

using TouchSocket.Core;

namespace ThingsGateway.Debug;

public partial class OpcUaMaster : IDisposable
{
    public LoggerGroup? LogMessage;
    private readonly OpcUaProperty OpcUaProperty = new();
    private ThingsGateway.Foundation.OpcUa.OpcUaMaster _plc;
    private string LogPath;
    private string RegisterAddress;
    private bool ShowSubvariable;
    private string WriteValue;

    /// <inheritdoc/>
    ~OpcUaMaster()
    {
        this.SafeDispose();
    }

    private DeviceComponent DeviceComponent { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    private IStringLocalizer<OpcUaProperty> OpcUaPropertyLocalizer { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _plc?.SafeDispose();
        GC.SuppressFinalize(this);
    }
    [Inject]
    private DownloadService DownloadService { get; set; }
    [Inject]
    private ToastService ToastService { get; set; }
    private async Task Export()
    {
        try
        {
            await _plc.CheckApplicationInstanceCertificate();
            string path = $"{AppContext.BaseDirectory}OPCUAClientCertificate/pki/trustedPeer/certs";
            Directory.CreateDirectory(path);
            var files = Directory.GetFiles(path);
            if (files.Length == 0)
            {
                return;
            }
            foreach (var item in files)
            {
                using var fileStream = new FileStream(item, FileMode.Open, FileAccess.Read);

                var extension = Path.GetExtension(item);
                extension ??= ".der";

                await DownloadService.DownloadFromStreamAsync($"ThingsGateway{extension}", fileStream);
            }
            await ToastService.Default();
        }
        catch (Exception ex)
        {
            await ToastService.Warn(ex);
        }

    }


    protected override void OnInitialized()
    {
        _plc = new ThingsGateway.Foundation.OpcUa.OpcUaMaster();
        _plc.OpcUaProperty = OpcUaProperty;

        LogMessage = new TouchSocket.Core.LoggerGroup() { LogLevel = TouchSocket.Core.LogLevel.Trace };
        var logger = TextFileLogger.GetMultipleFileLogger(_plc.GetHashCode().ToLong().GetDebugLogPath());
        logger.LogLevel = LogLevel.Trace;
        LogMessage.AddLogger(logger);

        _plc.LogEvent = (a, b, c, d) => LogMessage.Log((LogLevel)a, b, c, d);
        _plc.DataChangedHandler += (a) => LogMessage.Trace(a.ToSystemTextJsonString());
        base.OnInitialized();
    }

    private async Task Add()
    {
        try
        {
            if (_plc.Connected)
                await _plc.AddSubscriptionAsync(Guid.NewGuid().ToString(), [RegisterAddress]);
        }
        catch (Exception ex)
        {
            LogMessage?.LogWarning(ex);
        }
    }

    private async Task Connect()
    {
        try
        {
            _plc.Disconnect();
            LogPath = _plc?.GetHashCode().ToLong().GetDebugLogPath();
            await GetOpc().ConnectAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogMessage?.Log(LogLevel.Error, null, ex.Message, ex);
        }
    }

    private void Disconnect()
    {
        try
        {
            _plc.Disconnect();
        }
        catch (Exception ex)
        {
            LogMessage?.Log(LogLevel.Error, null, ex.Message, ex);
        }
    }

    private ThingsGateway.Foundation.OpcUa.OpcUaMaster GetOpc()
    {
        //载入配置
        _plc.OpcUaProperty = OpcUaProperty;
        return _plc;
    }

    private async Task ReadAsync()
    {
        if (_plc.Connected)
        {
            try
            {
                var data = await _plc.ReadJTokenValueAsync([RegisterAddress]);

                LogMessage?.LogInformation($" {data[0].Item1}：{data[0].Item3}");
            }
            catch (Exception ex)
            {
                LogMessage?.LogWarning(ex);
            }
        }
    }

    private async Task Remove()
    {
        if (_plc?.Connected == true)
            await _plc.RemoveSubscriptionAsync("");
    }

    private async Task ShowImport()
    {
        var op = new DialogOption()
        {
            IsScrolling = false,
            Title = OpcUaPropertyLocalizer["ShowImport"],
            ShowFooter = false,
            ShowCloseButton = false,
            Size = Size.ExtraLarge
        };
        op.Component = BootstrapDynamicComponent.CreateComponent<OpcUaImportVariable>(new Dictionary<string, object?>
        {
            [nameof(OpcUaImportVariable.Plc)] = _plc,
            [nameof(OpcUaImportVariable.ShowSubvariable)] = ShowSubvariable,
        });
        await DialogService.Show(op);
    }

    private async Task WriteAsync()
    {
        if (_plc.Connected)
        {
            var data = await _plc.WriteNodeAsync(
                new()
                {
                        {RegisterAddress, WriteValue.GetJTokenFromString()}
                }
                ).ConfigureAwait(false);

            foreach (var item in data)
            {
                if (item.Value.Item1)
                    LogMessage?.LogInformation(item.ToSystemTextJsonString());
                else
                    LogMessage?.LogWarning(item.ToSystemTextJsonString());
            }
        }
    }
}
