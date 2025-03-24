//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

namespace ThingsGateway.Gateway.Application;

internal static class ManageHelper
{

    /// <summary>
    /// 线程最大等待间隔时间
    /// </summary>
    public static volatile ChannelThreadOptions ChannelThreadOptions = App.GetOptions<ChannelThreadOptions>();


    public static void CheckChannelCount(int addCount)
    {
        if (GlobalData.Channels.Count+ addCount > ManageHelper.ChannelThreadOptions.MaxChannelCount)
        {
            throw new Exception($"The number of channels exceeds the limit {ManageHelper.ChannelThreadOptions.MaxChannelCount}");
        }
    }

    public static void CheckDeviceCount(int addCount)
    {
        if (GlobalData.Devices.Count + addCount > ManageHelper.ChannelThreadOptions.MaxDeviceCount)
        {
            throw new Exception($"The number of devices exceeds the limit {ManageHelper.ChannelThreadOptions.MaxDeviceCount}");
        }
    }


    public static void CheckVariableCount(int addCount)
    {
        if (GlobalData.IdVariables.Count + addCount > ManageHelper.ChannelThreadOptions.MaxVariableCount)
        {
            throw new Exception($"The number of variables exceeds the limit {ManageHelper.ChannelThreadOptions.MaxVariableCount}");
        }
    }

}