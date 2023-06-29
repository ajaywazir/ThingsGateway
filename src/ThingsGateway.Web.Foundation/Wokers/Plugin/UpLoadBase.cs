﻿#region copyright
//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://diego2098.gitee.io/thingsgateway-docs/
//  QQ群：605534569
//------------------------------------------------------------------------------
#endregion

using Microsoft.Extensions.Logging;

using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using TouchSocket.Core;

namespace ThingsGateway.Web.Foundation;


/// <summary>
/// 上传插件
/// <para></para>
/// 约定：
/// <para></para>
/// 如果设备属性需要密码输入，属性名称中需包含Password字符串
/// <para></para>
/// 如果设备属性需要大文本输入，属性名称中需包含BigText字符串
/// <br></br>
/// 因为自定义上传插件需求比较大，这里着重解释代码运行原理
/// 继承<see cref="UpLoadBase"/>后，可以看到需要实现各类虚方法/属性<br></br>
/// <see cref="UploadVariables"/> <br></br>
/// <see cref="VariablePropertys"/><br></br>
/// <see cref="DriverBase.DriverPropertys"/><br></br>
/// <see cref="BeforStartAsync"/><br></br>
/// <see cref="ExecuteAsync"/><br></br>
/// <see cref="DriverBase.IsConnected"/><br></br>
/// <see cref="Init(UploadDeviceRunTime)"/><br></br>
/// 含义可看注释，下面看看网关上传插件的生命周期<br></br>
/// 1、构造函数<see cref="UpLoadBase(IServiceScopeFactory)"/> 传入参数服务工厂，在需要获取服务时使用<see cref="DriverBase._scopeFactory"/><br></br>
/// 2、<see cref="Init(UploadDeviceRunTime)"/>初始化函数，传入上传设备参数，只执行一次，在这个方法内，一般会初始化一些必要的实例，比如new MqttClient，以及一些必要的实现属性，比如<see cref="UploadVariables"/><br></br>
/// 3、<see cref="BeforStartAsync"/>开始前执行的方法，比如连接mqtt等，只执行一次<br></br>
/// 4、<see cref="ExecuteAsync"/>核心执行的方法，需实现上传方法，在插件结束前会一直循环调用<br></br>
/// 5、<see cref="DisposableObject.Dispose(bool)"/> 结束时调用的方法，实现资源释放方法<br></br>
/// 网关的数据是如何传入到上传插件的，下面会以Mqtt上传为例<br></br>
/// 1、如何获取采集变量值？在初始化函数中<see cref="Init(UploadDeviceRunTime)"/>获取全局设备/变量<br></br>
/// 通过<see cref="DriverBase._scopeFactory"/>获取单例服务<see cref="GlobalCollectDeviceData"/><br></br>
/// 可以看到在这个单例服务中，已经拥有全部的采集设备与变量<br></br>
/// 2、如何获取采集变量中的上传属性？UploadBase中封装了通用方法<see cref="UpLoadBase.GetPropertyValue(CollectVariableRunTime, string)"/><br></br>
/// 比如定义了变量属性Enable，只有设置为true的变量才会用作某逻辑，执行方法GetPropertyValue(tag,"Enable")，也可用硬编码传入propertyName参数<br></br>
/// 3、如何定义自己的上传实体，第一步中获取获取单例服务<see cref="GlobalCollectDeviceData"/>，在拥有全局变量下，可以使用<see cref="Mapster"/> 或者 手动赋值到DTO实体<br></br>
/// 4、完整的参考可以查看MqttClient插件ThingsGateway\src\Plugins\ThingsGateway.Mqtt\ThingsGateway.Mqtt.csproj<br></br>
/// </summary>
public abstract class UpLoadBase : DriverBase
{

    /// <inheritdoc cref="UpLoadBase"/>
    public UpLoadBase(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    /// <summary>
    /// 返回插件的上传变量，一般在<see cref="Init(UploadDeviceRunTime)"/>后初始化
    /// </summary>
    public abstract List<CollectVariableRunTime> UploadVariables { get; }

    /// <summary>
    /// 插件配置项 ，继承实现<see cref="VariablePropertyBase"/>后，返回继承类，如果不存在，返回null
    /// </summary>
    public abstract VariablePropertyBase VariablePropertys { get; }



    /// <summary>
    /// 开始执行的方法
    /// </summary>
    /// <returns></returns>
    public abstract Task BeforStartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 循环执行
    /// </summary>
    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
    /// <summary>
    /// 当前上传设备
    /// </summary>
    public UploadDeviceRunTime CurDevice { get; protected set; }
    /// <summary>
    /// 初始化
    /// </summary>
    public void Init(ILogger logger, UploadDeviceRunTime device)
    {
        _logger = logger;
        IsLogOut = device.IsLogOut;
        CurDevice = device;
        Directory.CreateDirectory("Cache");
        GetCacheDb().DbMaintenance.CreateDatabase();//创建数据库
        GetCacheDb().CodeFirst.InitTables(typeof(CacheTable));
        Init(device);
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="device">设备</param>
    protected abstract void Init(UploadDeviceRunTime device);

    /// <summary>
    /// 获取变量的属性值
    /// </summary>
    public virtual string GetPropertyValue(CollectVariableRunTime variableRunTime, string propertyName)
    {
        if (variableRunTime == null)
            return null;
        if (variableRunTime.VariablePropertys.ContainsKey(CurDevice.Id))
        {
            var data = variableRunTime.VariablePropertys[CurDevice.Id].FirstOrDefault(a =>
                  a.PropertyName == propertyName);
            if (data != null)
            {
                return data.Value;
            }
        }
        return null;
    }
    /// <summary>
    /// 获取数据库链接
    /// </summary>
    /// <returns></returns>
    public SqlSugarClient GetCacheDb()
    {
        var configureExternalServices = new ConfigureExternalServices
        {
            EntityService = (type, column) => // 修改列可空-1、带?问号 2、String类型若没有Required
            {
                if ((type.PropertyType.IsGenericType && type.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    || (type.PropertyType == typeof(string) && type.GetCustomAttribute<RequiredAttribute>() == null))
                    column.IsNullable = true;
            },
        };

        var sqlSugarClient = new SqlSugarClient(new ConnectionConfig()
        {
            ConnectionString = $"Data Source=Cache/{CurDevice.Id}.db;",//连接字符串
            DbType = DbType.Sqlite,//数据库类型
            IsAutoCloseConnection = true, //不设成true要手动close
            ConfigureExternalServices = configureExternalServices,
        }
        );
        return sqlSugarClient;
    }

    /// <summary>
    /// 获取缓存表前十条
    /// </summary>
    /// <returns></returns>
    public async Task<List<CacheTable>> GetCacheData()
    {
        var db = GetCacheDb();
        var data = await db.Queryable<CacheTable>().Take(10).ToListAsync();
        return data;
    }
    /// <summary>
    /// 增加离线缓存，限制表最大默认2000行
    /// </summary>
    /// <returns></returns>
    public async Task<bool> AddCacheData(string topic, string data, int max = 2000)
    {
        var db = GetCacheDb();
        var count = await db.Queryable<CacheTable>().CountAsync();
        if (count > max)
        {
            var data1 = await db.Queryable<CacheTable>().OrderBy(a => a.Id).Take(count - max).ToListAsync();
            await db.Deleteable(data1).ExecuteCommandAsync();
        }
        var result = await db.Insertable(new CacheTable() { Id = YitIdHelper.NextId(), Topic = topic, CacheStr = data }).ExecuteCommandAsync();
        return result > 0;
    }
    /// <summary>
    /// 清除离线缓存
    /// </summary>
    /// <returns></returns>
    public async Task<bool> DeleteCacheData(params long[] data)
    {
        var db = GetCacheDb();
        var result = await db.Deleteable<CacheTable>().In(data).ExecuteCommandAsync();
        return result > 0;
    }
}
/// <summary>
/// 缓存表
/// </summary>
public class CacheTable
{
    /// <summary>
    /// Id
    /// </summary>
    [SugarColumn(IsPrimaryKey = true)]
    public long Id { get; set; }
    /// <summary>
    /// Topic
    /// </summary>
    public string Topic { get; set; }
    /// <summary>
    /// 缓存值
    /// </summary>
    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string CacheStr { get; set; }
}
