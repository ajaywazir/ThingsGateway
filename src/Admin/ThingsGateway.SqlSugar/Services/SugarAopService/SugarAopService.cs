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

using SqlSugar;

namespace ThingsGateway.Admin.Application;

public class SugarAopService : ISugarAopService
{
    private IClaimsPrincipalService _claimsPrincipalService;
    public SugarAopService(IClaimsPrincipalService appService)
    {
        _claimsPrincipalService = appService;
    }
    /// <summary>
    /// Aop设置
    /// </summary>
    public void AopSetting(ISqlSugarClient db, bool? isShowSql = null)
    {
        var config = db.CurrentConnectionConfig;

        if ((isShowSql == null && App.HostEnvironment.IsDevelopment()) || isShowSql == true)
        {
            // 打印SQL语句
            db.Aop.OnLogExecuting = (sql, pars) =>
            {
                if (sql.StartsWith("SELECT"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    DbContext.WriteLog($"查询{config.ConfigId}库操作");
                }
                if (sql.StartsWith("UPDATE"))
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    DbContext.WriteLog($"修改{config.ConfigId}库操作");
                }
                if (sql.StartsWith("INSERT"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    DbContext.WriteLog($"添加{config.ConfigId}库操作");
                }
                if (sql.StartsWith("DELETE"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    DbContext.WriteLog($"删除{config.ConfigId}库操作");
                }
                DbContext.WriteLogWithSql(UtilMethods.GetNativeSql(sql, pars));
                DbContext.WriteLog($"{config.ConfigId}库操作结束");
                Console.ForegroundColor = ConsoleColor.White;
            };
        }
        //异常
        db.Aop.OnError = (ex) =>
        {
            if (ex.Parametres == null) return;
            Console.ForegroundColor = ConsoleColor.Red;
            DbContext.WriteLog($"{config.ConfigId}库操作异常");
            DbContext.WriteErrorLogWithSql(UtilMethods.GetNativeSql(ex.Sql, (SugarParameter[])ex.Parametres));
            NewLife.Log.XTrace.WriteException(ex);
            Console.ForegroundColor = ConsoleColor.White;
        };
        //插入和更新过滤器
        db.Aop.DataExecuting = (oldValue, entityInfo) =>
        {
            // 新增操作
            if (entityInfo.OperationType == DataFilterType.InsertByObject)
            {
                // 主键(long类型)且没有值的---赋值雪花Id
                if (entityInfo.EntityColumnInfo.IsPrimarykey && entityInfo.EntityColumnInfo.PropertyInfo.PropertyType == typeof(long))
                {
                    var id = entityInfo.EntityColumnInfo.PropertyInfo.GetValue(entityInfo.EntityValue);
                    if (id == null || (long)id == 0)
                        entityInfo.SetValue(CommonUtils.GetSingleId());
                }
                if (entityInfo.PropertyName == nameof(BaseEntity.CreateTime))
                    entityInfo.SetValue(DateTime.Now);

                if (_claimsPrincipalService.User != null)
                {
                    //创建人
                    if (entityInfo.PropertyName == nameof(BaseEntity.CreateUserId))
                        entityInfo.SetValue(UserManager.UserId);
                    if (entityInfo.PropertyName == nameof(BaseEntity.CreateUser))
                        entityInfo.SetValue(UserManager.UserAccount);
                    if (entityInfo.PropertyName == nameof(BaseDataEntity.CreateOrgId))
                        entityInfo.SetValue(UserManager.OrgId);
                }
            }
            // 更新操作
            if (entityInfo.OperationType == DataFilterType.UpdateByObject)
            {
                //更新时间
                if (entityInfo.PropertyName == nameof(BaseEntity.UpdateTime))
                    entityInfo.SetValue(DateTime.Now);
                //更新人
                if (_claimsPrincipalService.User != null)
                {
                    if (entityInfo.PropertyName == nameof(BaseEntity.UpdateUserId))
                        entityInfo.SetValue(UserManager.UserId);
                    if (entityInfo.PropertyName == nameof(BaseEntity.UpdateUser))
                        entityInfo.SetValue(UserManager.UserAccount);
                }
            }
        };

        //查询数据转换
        db.Aop.DataExecuted = (value, entity) =>
        {
        };


        db.Aop.OnLogExecuted = (sql, pars) =>
        {
            //执行时间超过1秒
            if (db.Ado.SqlExecutionTime.TotalSeconds > 1)
            {
                //代码CS文件名
                var fileName = db.Ado.SqlStackTrace.FirstFileName;
                //代码行数
                var fileLine = db.Ado.SqlStackTrace.FirstLine;
                //方法名
                var FirstMethodName = db.Ado.SqlStackTrace.FirstMethodName;

                DbContext.WriteLog($"{fileName}-{FirstMethodName}-{fileLine} 执行时间超过1秒");
                DbContext.WriteLogWithSql(UtilMethods.GetNativeSql(sql, pars));

            }
        };
    }

}


public class SugarConfigAopService : ISugarConfigAopService
{
    public SqlSugarOptions Config(SqlSugarOptions sqlSugarOptions)
    {
        return sqlSugarOptions;
    }
}
