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

using System.Reflection;

namespace ThingsGateway.Gateway.Application;

public static class ChannelServiceHelpers
{

    public static USheetDatas ExportChannel(IEnumerable<Channel> models)
    {
        var data = ExportChannelCore(models);
        return USheetDataHelpers.GetUSheetDatas(data);

    }


    internal static Dictionary<string, IList<Dictionary<string, object>>> ExportChannelCore(IEnumerable<Channel>? data)
    {
        //总数据
        Dictionary<string, IList<Dictionary<string, object>>> sheets = new();
        //通道页
        List<Dictionary<string, object>> channelExports = new();

        #region 列名称

        var type = typeof(Channel);
        var propertyInfos = type.GetRuntimeProperties().Where(a => a.GetCustomAttribute<IgnoreExcelAttribute>() == null)
             .OrderBy(
            a =>
            {
                var order = a.GetCustomAttribute<AutoGenerateColumnAttribute>()?.Order ?? int.MaxValue; ;
                if (order < 0)
                {
                    order = order + 10000000;
                }
                else if (order == 0)
                {
                    order = 10000000;
                }
                return order;
            }
            )
            ;

        #endregion 列名称

        foreach (var device in data)
        {
            Dictionary<string, object> channelExport = new();
            foreach (var item in propertyInfos)
            {
                //描述
                var desc = type.GetPropertyDisplayName(item.Name);
                //数据源增加
                channelExport.Add(desc ?? item.Name, item.GetValue(device)?.ToString());
            }

            //添加完整设备信息
            channelExports.Add(channelExport);
        }
        //添加设备页
        sheets.Add(ExportString.ChannelName, channelExports);
        return sheets;
    }


    public static async Task<Dictionary<string, ImportPreviewOutputBase>> ImportAsync(USheetDatas uSheetDatas)
    {
        try
        {
            var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
            var channelDicts = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);
            //导入检验结果
            Dictionary<string, ImportPreviewOutputBase> ImportPreviews = new();
            //设备页
            ImportPreviewOutput<Channel> channelImportPreview = new();

            var sheetNames = uSheetDatas.sheets.Keys.ToList();
            foreach (var sheetName in sheetNames)
            {

                List<IDictionary<string, object>> rows = new();
                var first = uSheetDatas.sheets[sheetName].cellData[0];

                foreach (var item in uSheetDatas.sheets[sheetName].cellData)
                {
                    if (item.Key == 0)
                    {
                        continue;
                    }
                    var expando = new Dictionary<string, object>();
                    foreach (var keyValue in item.Value)
                    {
                        expando.Add(first[keyValue.Key].v?.ToString(), keyValue.Value.v);
                    }
                    rows.Add(expando);
                }

                GlobalData.ChannelService.SetChannelData(dataScope, channelDicts, ImportPreviews, channelImportPreview, sheetName, rows);
                if (channelImportPreview.HasError)
                {
                    throw new(channelImportPreview.Results.FirstOrDefault(a => !a.Success).ErrorMessage ?? "error");
                }
            }

            return ImportPreviews;
        }
        finally
        {
        }
    }

}