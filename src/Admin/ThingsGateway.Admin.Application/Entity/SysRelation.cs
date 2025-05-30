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
/// 系统关系表
///</summary>
[SugarTable("sys_relation", TableDescription = "系统关系表")]
[Tenant(SqlSugarConst.DB_Admin)]
public class SysRelation : PrimaryKeyEntity
{
    /// <summary>
    /// 分类
    ///</summary>
    [SugarColumn(ColumnDescription = "分类")]
    public RelationCategoryEnum Category { get; set; }

    /// <summary>
    /// 对象ID
    ///</summary>
    [SugarColumn(ColumnDescription = "对象ID")]
    public long ObjectId { get; set; }

    /// <summary>
    /// 目标ID
    ///</summary>
    [SugarColumn(ColumnDescription = "目标ID", IsNullable = true)]
    public string? TargetId { get; set; }
}
