﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------


using SqlSugar;

namespace ThingsGateway.Admin.Application;


/// <summary>
/// Aop设置
/// </summary>
public interface ISugarAopService
{
    /// <summary>
    /// Aop设置
    /// </summary>
    public void AopSetting(ISqlSugarClient db, bool? isShowSql = null);

}
/// <summary>
/// Aop设置，可自定义加解密等
/// </summary>
public interface ISugarConfigAopService
{
    public SqlSugarOptions Config(SqlSugarOptions sqlSugarOptions);
}
