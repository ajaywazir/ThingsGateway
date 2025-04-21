//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using BootstrapBlazor.Components;

using Mapster;

using Microsoft.AspNetCore.Components.Forms;

using MiniExcelLibs;

using SqlSugar;

using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection;
using System.Text;

using ThingsGateway.Extension.Generic;
using ThingsGateway.Foundation.Extension.Dynamic;
using ThingsGateway.FriendlyException;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

internal sealed class ChannelService : BaseService<Channel>, IChannelService
{
    private readonly IDispatchService<Channel> _dispatchService;

    /// <inheritdoc cref="IChannelService"/>
    public ChannelService(
    IDispatchService<Channel>? dispatchService
        )
    {
        _dispatchService = dispatchService;
    }

    #region CURD

    /// <inheritdoc/>
    [OperDesc("CopyChannel", localizerType: typeof(Channel), isRecordPar: false)]
    public async Task<bool> CopyAsync(List<Channel> models, Dictionary<Device, List<Variable>> devices)
    {
        using var db = GetDB();

        //事务
        var result = await db.UseTranAsync(async () =>
        {
            ManageHelper.CheckChannelCount(models.Count);

            await db.Insertable(models).ExecuteCommandAsync().ConfigureAwait(false);

            var device = devices.Keys.ToList();
            ManageHelper.CheckDeviceCount(device.Count);

            await db.Insertable(device).ExecuteCommandAsync().ConfigureAwait(false);

            var variable = devices.SelectMany(a => a.Value).ToList();
            ManageHelper.CheckVariableCount(variable.Count);

            await db.Insertable(variable).ExecuteCommandAsync().ConfigureAwait(false);


        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            DeleteChannelFromCache();
            App.GetService<IDeviceService>().DeleteDeviceFromCache();
            App.GetService<IVariableService>().DeleteVariableCache();
            return true;
        }
        else
        {
            //写日志
            throw new(result.ErrorMessage, result.ErrorException);
        }

    }


    public async Task UpdateLogAsync(long channelId, LogLevel logLevel)
    {
        using var db = GetDB();

        //事务
        var result = await db.UseTranAsync(async () =>
        {
            //更新数据库

            await db.Updateable<Channel>().SetColumns(it => new Channel() { LogLevel = logLevel }).Where(a => a.Id == channelId).ExecuteCommandAsync().ConfigureAwait(false);

        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            DeleteChannelFromCache();
        }
        else
        {
            //写日志
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    /// <inheritdoc/>
    [OperDesc("SaveChannel", localizerType: typeof(Channel), isRecordPar: false)]
    public async Task<bool> BatchEditAsync(IEnumerable<Channel> models, Channel oldModel, Channel model)
    {
        var differences = models.GetDiffProperty(oldModel, model);
        if (differences?.Count > 0)
        {
            using var db = GetDB();
            var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);

            //事务
            var result = await db.UseTranAsync(async () =>
            {
                var data = models
                            .WhereIf(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
             .WhereIf(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
             .ToList();

                //更新数据库
                await db.Updateable(data).UpdateColumns(differences.Select(a => a.Key).ToArray()).ExecuteCommandAsync().ConfigureAwait(false);

            }).ConfigureAwait(false);
            if (result.IsSuccess)//如果成功了
            {
                DeleteChannelFromCache();
                return true;
            }
            else
            {
                //写日志
                throw new(result.ErrorMessage, result.ErrorException);
            }
        }
        else
        {
            return true;
        }
    }


    [OperDesc("DeleteChannel", localizerType: typeof(Channel), isRecordPar: false)]
    public async Task<bool> DeleteChannelAsync(IEnumerable<long> ids)
    {
        var deviceService = App.RootServices.GetRequiredService<IDeviceService>();
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);

        using var db = GetDB();
        //事务
        var result = await db.UseTranAsync(async () =>
        {
            await db.Deleteable<Channel>()
              .WhereIF(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
             .WhereIF(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
            .Where(a => ids.Contains(a.Id)).ExecuteCommandAsync().ConfigureAwait(false);
            await deviceService.DeleteByChannelIdAsync(ids, db).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            DeleteChannelFromCache();
            return true;
        }
        else
        {
            //写日志
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    /// <inheritdoc />
    public void DeleteChannelFromCache()
    {
        App.CacheService.Remove(ThingsGatewayCacheConst.Cache_Channel);//删除通道缓存
        _dispatchService.Dispatch(new());
    }

    /// <summary>
    /// 从缓存/数据库获取全部信息
    /// </summary>
    /// <returns>列表</returns>
    public async Task<List<Channel>> GetAllAsync(SqlSugarClient db = null)
    {
        var key = ThingsGatewayCacheConst.Cache_Channel;
        var channels = App.CacheService.Get<List<Channel>>(key);
        if (channels == null)
        {
            db ??= GetDB();
            channels = await db.Queryable<Channel>().OrderBy(a => a.Id).ToListAsync().ConfigureAwait(false);
            App.CacheService.Set(key, channels);
        }
        return channels;
    }

    /// <summary>
    /// 报表查询
    /// </summary>
    /// <param name="exportFilter">查询条件</param>
    public async Task<QueryData<Channel>> PageAsync(ExportFilter exportFilter)
    {
        HashSet<long>? channel = null;
        if (exportFilter.PluginType != null)
        {
            var pluginInfo = GlobalData.PluginService.GetList(exportFilter.PluginType).Select(a => a.FullName).ToHashSet();
            channel = (await GetAllAsync().ConfigureAwait(false)).Where(a => pluginInfo.Contains(a.PluginName)).Select(a => a.Id).ToHashSet();
        }
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
        return await QueryAsync(exportFilter.QueryPageOptions, a => a
        .WhereIF(!exportFilter.QueryPageOptions.SearchText.IsNullOrWhiteSpace(), a => a.Name.Contains(exportFilter.QueryPageOptions.SearchText!))
                .WhereIF(!exportFilter.PluginName.IsNullOrWhiteSpace(), a => a.PluginName == exportFilter.PluginName)
                        .WhereIF(channel != null, a => channel.Contains(a.Id))
                        .WhereIF(exportFilter.ChannelId != null, a => a.Id == exportFilter.ChannelId)

                          .WhereIF(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
         .WhereIF(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)

       , exportFilter.FilterKeyValueAction).ConfigureAwait(false);
    }

    /// <summary>
    /// 保存通道
    /// </summary>
    /// <param name="input">通道</param>
    /// <param name="type">保存类型</param>
    [OperDesc("SaveChannel", localizerType: typeof(Channel))]
    public async Task<bool> SaveChannelAsync(Channel input, ItemChangedType type)
    {
        if ((await GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name).TryGetValue(input.Name, out var channel))
        {
            if (channel.Id != input.Id)
            {
                throw Oops.Bah(Localizer["NameDump", channel.Name]);
            }
        }

        if (type == ItemChangedType.Update)
            await GlobalData.SysUserService.CheckApiDataScopeAsync(input.CreateOrgId, input.CreateUserId).ConfigureAwait(false);
        else
            ManageHelper.CheckChannelCount(1);

        if (await base.SaveAsync(input, type).ConfigureAwait(false))
        {
            DeleteChannelFromCache();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 保存通道
    /// </summary>
    /// <param name="input">通道</param>
    /// <param name="type">保存类型</param>
    [OperDesc("SaveChannel", localizerType: typeof(Channel), isRecordPar: false)]
    public async Task<bool> BatchSaveAsync(List<Channel> input, ItemChangedType type)
    {
        if (type == ItemChangedType.Update)
            await GlobalData.SysUserService.CheckApiDataScopeAsync(input.Select(a => a.CreateOrgId), input.Select(a => a.CreateUserId)).ConfigureAwait(false);
        else
            ManageHelper.CheckDeviceCount(input.Count);

        if (await base.SaveAsync(input, type).ConfigureAwait(false))
        {
            DeleteChannelFromCache();
            return true;
        }
        return false;
    }




    #endregion

    #region 导出

    /// <inheritdoc/>
    [OperDesc("ExportChannel", isRecordPar: false, localizerType: typeof(Channel))]
    public async Task<Dictionary<string, object>> ExportChannelAsync(ExportFilter exportFilter)
    {
        var data = await PageAsync(exportFilter).ConfigureAwait(false);
        return ChannelServiceHelpers.ExportChannelCore(data.Items);
    }

    /// <inheritdoc/>
    [OperDesc("ExportChannel", isRecordPar: false, localizerType: typeof(Channel))]
    public async Task<MemoryStream> ExportMemoryStream(List<Channel> data)
    {
        var sheets = ChannelServiceHelpers.ExportChannelCore(data);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(sheets).ConfigureAwait(false);
        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream;
    }



    #endregion 导出

    #region 导入

    /// <inheritdoc/>
    [OperDesc("ImportChannel", isRecordPar: false, localizerType: typeof(Channel))]
    public async Task<HashSet<long>> ImportChannelAsync(Dictionary<string, ImportPreviewOutputBase> input)
    {
        var channels = new List<Channel>();
        foreach (var item in input)
        {
            if (item.Key == ExportString.ChannelName)
            {
                var channelImports = ((ImportPreviewOutput<Channel>)item.Value).Data;
                channels = channelImports.Select(a => a.Value).ToList();
                break;
            }
        }
        var upData = channels.Where(a => a.IsUp).ToList();
        var insertData = channels.Where(a => !a.IsUp).ToList();

        ManageHelper.CheckChannelCount(insertData.Count);

        using var db = GetDB();
        await db.BulkCopyAsync(insertData, 100000).ConfigureAwait(false);
        await db.BulkUpdateAsync(upData, 100000).ConfigureAwait(false);
        DeleteChannelFromCache();
        return channels.Select(a => a.Id).ToHashSet();
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, ImportPreviewOutputBase>> PreviewAsync(IBrowserFile browserFile)
    {
        var path = await browserFile.StorageLocal().ConfigureAwait(false);
        try
        {
            var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
            var sheetNames = MiniExcel.GetSheetNames(path);
            var channelDicts = (await GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);
            //导入检验结果
            Dictionary<string, ImportPreviewOutputBase> ImportPreviews = new();
            //设备页
            ImportPreviewOutput<Channel> channelImportPreview = new();
            foreach (var sheetName in sheetNames)
            {
                var rows = MiniExcel.Query(path, useHeaderRow: true, sheetName: sheetName).Cast<IDictionary<string, object>>();
                SetChannelData(dataScope, channelDicts, ImportPreviews, channelImportPreview, sheetName, rows);
            }

            return ImportPreviews;
        }
        finally
        {
            FileUtility.Delete(path);
        }
    }

    public void SetChannelData(HashSet<long>? dataScope, Dictionary<string, Channel> channelDicts, Dictionary<string, ImportPreviewOutputBase> ImportPreviews, ImportPreviewOutput<Channel> channelImportPreview, string sheetName, IEnumerable<IDictionary<string, object>> rows)
    {
        #region sheet

        if (sheetName == ExportString.ChannelName)
        {
            int row = 1;
            ImportPreviewOutput<Channel> importPreviewOutput = new();
            ImportPreviews.Add(sheetName, importPreviewOutput);
            channelImportPreview = importPreviewOutput;
            List<Channel> channels = new();
            var type = typeof(Channel);
            // 获取目标类型的所有属性，并根据是否需要过滤 IgnoreExcelAttribute 进行筛选
            var channelProperties = type.GetRuntimeProperties().Where(a => (a.GetCustomAttribute<IgnoreExcelAttribute>() == null) && a.CanWrite)
                                        .ToDictionary(a => type.GetPropertyDisplayName(a.Name), a => (a, a.IsNullableType()));

            rows.ForEach(item =>
            {
                try
                {
                    var channel = item.ConvertToEntity<Channel>(channelProperties);
                    if (channel == null)
                    {
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((row++, false, Localizer["ImportNullError"]));
                        return;
                    }

                    // 进行对象属性的验证
                    var validationContext = new ValidationContext(channel);
                    var validationResults = new List<ValidationResult>();
                    validationContext.ValidateProperty(validationResults);

                    // 构建验证结果的错误信息
                    StringBuilder stringBuilder = new();
                    foreach (var validationResult in validationResults.Where(v => !string.IsNullOrEmpty(v.ErrorMessage)))
                    {
                        foreach (var memberName in validationResult.MemberNames)
                        {
                            stringBuilder.Append(validationResult.ErrorMessage!);
                        }
                    }

                    // 如果有验证错误，则添加错误信息到导入预览结果并返回
                    if (stringBuilder.Length > 0)
                    {
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((row++, false, stringBuilder.ToString()));
                        return;
                    }

                    if (channelDicts.TryGetValue(channel.Name, out var collectChannel))
                    {
                        channel.Id = collectChannel.Id;
                        channel.CreateOrgId = collectChannel.CreateOrgId;
                        channel.CreateUserId = collectChannel.CreateUserId;
                        channel.IsUp = true;
                    }
                    else
                    {
                        channel.Id = CommonUtils.GetSingleId();
                        channel.IsUp = false;
                        channel.CreateOrgId = UserManager.OrgId;
                        channel.CreateUserId = UserManager.UserId;
                    }

                    if (channel.IsUp && ((dataScope != null && dataScope?.Count > 0 && !dataScope.Contains(channel.CreateOrgId)) || dataScope?.Count == 0 && channel.CreateUserId != UserManager.UserId))
                    {
                        importPreviewOutput.Results.Add((row++, false, "Operation not permitted"));
                    }
                    else
                    {
                        channels.Add(channel);
                        importPreviewOutput.Results.Add((row++, true, null));
                    }
                    return;
                }
                catch (Exception ex)
                {
                    importPreviewOutput.HasError = true;
                    importPreviewOutput.Results.Add((row++, false, ex.Message));
                    return;
                }
            });
            importPreviewOutput.Data = channels.ToDictionary(a => a.Name);
        }

        #endregion sheet
    }





    #endregion 导入
}
