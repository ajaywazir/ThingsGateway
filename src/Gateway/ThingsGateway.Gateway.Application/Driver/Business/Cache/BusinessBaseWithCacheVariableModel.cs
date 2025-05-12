//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Collections.Concurrent;

using ThingsGateway.Extension;
using ThingsGateway.Foundation.Extension.Generic;

namespace ThingsGateway.Gateway.Application;

/// <summary>
/// 业务插件，实现实体VarModel缓存
/// </summary>
public abstract class BusinessBaseWithCacheVariableModel<VarModel> : BusinessBase
{
    protected ConcurrentQueue<CacheDBItem<VarModel>> _memoryVarModelQueue = new();
    protected ConcurrentQueue<CacheDBItem<List<VarModel>>> _memoryVarModelsQueue = new();
    protected volatile bool success = true;
    private volatile bool LocalDBCacheVarModelInited;
    private volatile bool LocalDBCacheVarModelsInited;
    protected sealed override BusinessPropertyBase _businessPropertyBase => _businessPropertyWithCache;

    protected abstract BusinessPropertyWithCache _businessPropertyWithCache { get; }

    /// <summary>
    /// 入缓存
    /// </summary>
    /// <param name="data"></param>
    protected virtual void AddCache(List<CacheDBItem<VarModel>> data)
    {
        if (_businessPropertyWithCache.CacheEnable && data?.Count > 0)
        {
            try
            {
                LogMessage?.LogInformation($"Add {typeof(VarModel).Name} data to file cache, count {data.Count}");
                foreach (var item in data)
                {
                    item.Id = CommonUtils.GetSingleId();
                }
                var dir = CacheDBUtil.GetCacheFilePath(CurrentDevice.Name.ToString());
                var fileStart = CacheDBUtil.GetFileName($"{CurrentDevice.PluginName}_{typeof(VarModel).Name}");
                var fullName = dir.CombinePathWithOs($"{fileStart}{CacheDBUtil.EX}");

                lock (this)
                {
                    bool s = false;
                    while (!s)
                    {
                        s = CacheDBUtil.DeleteCache(_businessPropertyWithCache.CacheFileMaxLength, fullName);
                    }
                    LocalDBCacheVarModelInited = false;
                    using var cache = LocalDBCacheVarModel();
                    cache.DBProvider.Fastest<CacheDBItem<VarModel>>().PageSize(50000).BulkCopy(data);
                }
            }
            catch
            {
                try
                {
                    using var cache = LocalDBCacheVarModel();
                    lock (cache.CacheDBOption.FileFullName)
                        cache.DBProvider.Fastest<CacheDBItem<VarModel>>().PageSize(50000).BulkCopy(data);
                }
                catch (Exception ex)
                {
                    LogMessage.LogWarning(ex, "Add cache fail");
                }
            }
        }
    }
    /// <summary>
    /// 入缓存
    /// </summary>
    /// <param name="data"></param>
    protected virtual void AddCache(List<CacheDBItem<List<VarModel>>> data)
    {
        if (_businessPropertyWithCache.CacheEnable && data?.Count > 0)
        {
            try
            {
                foreach (var item in data)
                {
                    item.Id = CommonUtils.GetSingleId();
                }
                var dir = CacheDBUtil.GetCacheFilePath(CurrentDevice.Name.ToString());
                var fileStart = CacheDBUtil.GetFileName($"{CurrentDevice.PluginName}_List_{typeof(VarModel).Name}");
                var fullName = dir.CombinePathWithOs($"{fileStart}{CacheDBUtil.EX}");

                lock (this)
                {
                    bool s = false;
                    while (!s)
                    {
                        s = CacheDBUtil.DeleteCache(_businessPropertyWithCache.CacheFileMaxLength, fullName);
                    }
                    LocalDBCacheVarModelsInited = false;
                    using var cache = LocalDBCacheVarModels();
                    cache.DBProvider.Fastest<CacheDBItem<List<VarModel>>>().PageSize(50000).BulkCopy(data);
                }
            }
            catch
            {
                try
                {
                    using var cache = LocalDBCacheVarModels();
                    lock (cache.CacheDBOption.FileFullName)
                        cache.DBProvider.Fastest<CacheDBItem<List<VarModel>>>().PageSize(50000).BulkCopy(data);
                }
                catch (Exception ex)
                {
                    LogMessage.LogWarning(ex, "Add cache fail");
                }
            }
        }
    }
    /// <summary>
    /// 添加队列，超限后会入缓存
    /// </summary>
    /// <param name="data"></param>
    protected virtual void AddQueueVarModel(CacheDBItem<VarModel> data)
    {
        if (_businessPropertyWithCache.CacheEnable)
        {
            //检测队列长度，超限存入缓存数据库
            if (_memoryVarModelQueue.Count > _businessPropertyWithCache.QueueMaxCount)
            {
                List<CacheDBItem<VarModel>> list = null;
                lock (_memoryVarModelQueue)
                {
                    if (_memoryVarModelQueue.Count > _businessPropertyWithCache.QueueMaxCount)
                    {
                        list = _memoryVarModelQueue.ToListWithDequeue();
                    }
                }
                AddCache(list);
            }
        }
        if (_memoryVarModelQueue.Count > _businessPropertyWithCache.QueueMaxCount)
        {
            lock (_memoryVarModelQueue)
            {
                if (_memoryVarModelQueue.Count > _businessPropertyWithCache.QueueMaxCount)
                {
                    LogMessage?.LogWarning($"{typeof(VarModel).Name} Queue exceeds limit, clear old data. If it doesn't work as expected, increase [QueueMaxCount] or Enable cache");
                    _memoryVarModelQueue.Clear();
                    _memoryVarModelQueue.Enqueue(data);
                    return;
                }
            }
        }
        else
        {
            _memoryVarModelQueue.Enqueue(data);
        }
    }

    /// <summary>
    /// 添加队列，超限后会入缓存
    /// </summary>
    /// <param name="data"></param>
    protected virtual void AddQueueVarModel(CacheDBItem<List<VarModel>> data)
    {
        if (_businessPropertyWithCache.CacheEnable)
        {
            //检测队列长度，超限存入缓存数据库
            if (_memoryVarModelsQueue.Count > _businessPropertyWithCache.QueueMaxCount)
            {
                List<CacheDBItem<List<VarModel>>> list = null;
                lock (_memoryVarModelsQueue)
                {
                    if (_memoryVarModelsQueue.Count > _businessPropertyWithCache.QueueMaxCount)
                    {
                        list = _memoryVarModelsQueue.ToListWithDequeue();
                    }
                }
                AddCache(list);
            }
        }
        if (_memoryVarModelsQueue.Count > _businessPropertyWithCache.QueueMaxCount)
        {
            lock (_memoryVarModelsQueue)
            {
                if (_memoryVarModelsQueue.Count > _businessPropertyWithCache.QueueMaxCount)
                {
                    _memoryVarModelsQueue.Clear();
                    _memoryVarModelsQueue.Enqueue(data);
                    return;
                }
            }
        }
        else
        {
            _memoryVarModelsQueue.Enqueue(data);
        }
    }


    /// <summary>
    /// 获取缓存对象，注意using
    /// </summary>
    protected virtual CacheDB LocalDBCacheVarModel()
    {
        var cacheDb = CacheDBUtil.GetCache(typeof(CacheDBItem<VarModel>), CurrentDevice.Name.ToString(), $"{CurrentDevice.PluginName}_{typeof(VarModel).Name}");
        if (!LocalDBCacheVarModelInited)
        {
            cacheDb.InitDb();
            LocalDBCacheVarModelInited = true;
        }
        return cacheDb;
    }

    /// <summary>
    /// 获取缓存对象，注意using
    /// </summary>
    protected virtual CacheDB LocalDBCacheVarModels()
    {
        var cacheDb = CacheDBUtil.GetCache(typeof(CacheDBItem<List<VarModel>>), CurrentDevice.Name.ToString(), $"{CurrentDevice.PluginName}_List_{typeof(VarModel).Name}");
        if (!LocalDBCacheVarModelsInited)
        {
            cacheDb.InitDb();
            LocalDBCacheVarModelsInited = true;
        }
        return cacheDb;
    }
    protected virtual async Task Update(CancellationToken cancellationToken)
    {
        await UpdateVarModelMemory(cancellationToken).ConfigureAwait(false);
        await UpdateVarModelsMemory(cancellationToken).ConfigureAwait(false);
        await UpdateVarModelCache(cancellationToken).ConfigureAwait(false);
        await UpdateVarModelsCache(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 需实现上传到通道
    /// </summary>
    /// <param name="item"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract ValueTask<OperResult> UpdateVarModel(IEnumerable<CacheDBItem<VarModel>> item, CancellationToken cancellationToken);
    /// <summary>
    /// 需实现上传到通道
    /// </summary>
    /// <param name="item"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract ValueTask<OperResult> UpdateVarModels(IEnumerable<VarModel> item, CancellationToken cancellationToken);
    protected async Task UpdateVarModelCache(CancellationToken cancellationToken)
    {
        if (_businessPropertyWithCache.CacheEnable)
        {
            #region //成功上传时，补上传缓存数据

            if (IsConnected())
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        //循环获取
                        using var cache = LocalDBCacheVarModel();
                        var varList = await cache.DBProvider.Queryable<CacheDBItem<VarModel>>().Take(_businessPropertyWithCache.SplitSize).ToListAsync(cancellationToken).ConfigureAwait(false);
                        if (varList.Count > 0)
                        {
                            try
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    var result = await UpdateVarModel(varList, cancellationToken).ConfigureAwait(false);
                                    if (result.IsSuccess)
                                    {
                                        //删除缓存
                                        await cache.DBProvider.Deleteable<CacheDBItem<VarModel>>(varList).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                                    }
                                    else
                                        break;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                if (success)
                                    LogMessage?.LogWarning(ex);
                                success = false;
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (success)
                        LogMessage?.LogWarning(ex);
                    success = false;
                }
            }

            #endregion //成功上传时，补上传缓存数据
        }
    }

    protected async Task UpdateVarModelsCache(CancellationToken cancellationToken)
    {
        if (_businessPropertyWithCache.CacheEnable)
        {
            #region //成功上传时，补上传缓存数据

            if (IsConnected())
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        //循环获取
                        using var cache = LocalDBCacheVarModels();
                        var varList = await cache.DBProvider.Queryable<CacheDBItem<List<VarModel>>>().FirstAsync(cancellationToken).ConfigureAwait(false);
                        if (varList?.Value?.Count > 0)
                        {
                            try
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    var result = await UpdateVarModels(varList.Value, cancellationToken).ConfigureAwait(false);
                                    if (result.IsSuccess)
                                    {
                                        //删除缓存
                                        await cache.DBProvider.Deleteable<CacheDBItem<List<VarModel>>>(varList).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                                    }
                                    else
                                        break;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                if (success)
                                    LogMessage?.LogWarning(ex);
                                success = false;
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (success)
                        LogMessage?.LogWarning(ex);
                    success = false;
                }
            }

            #endregion //成功上传时，补上传缓存数据
        }
    }

    protected async Task UpdateVarModelMemory(CancellationToken cancellationToken)
    {
        #region //上传变量内存队列中的数据

        try
        {
            var list = _memoryVarModelQueue.ToListWithDequeue();
            if (list?.Count > 0)
            {
                var data = list.ChunkBetter(_businessPropertyWithCache.SplitSize);
                foreach (var item in data)
                {
                    try
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            var result = await UpdateVarModel(item, cancellationToken).ConfigureAwait(false);
                            if (!result.IsSuccess)
                            {
                                AddCache(item.ToList());
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (success)
                            LogMessage?.LogWarning(ex);
                        success = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (success)
                LogMessage?.LogWarning(ex);
            success = false;
        }

        #endregion //上传变量内存队列中的数据
    }

    protected async Task UpdateVarModelsMemory(CancellationToken cancellationToken)
    {
        #region //上传变量内存队列中的数据

        try
        {
            if (_memoryVarModelsQueue.TryDequeue(out var cacheDBItem))
            {
                var list = cacheDBItem.Value;
                var data = list.ChunkBetter(_businessPropertyWithCache.SplitSize);
                foreach (var item in data)
                {
                    try
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            var result = await UpdateVarModels(item, cancellationToken).ConfigureAwait(false);
                            if (!result.IsSuccess)
                            {

                                AddCache(new List<CacheDBItem<List<VarModel>>>() { new CacheDBItem<List<VarModel>>(item.ToList()) });
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (success)
                            LogMessage?.LogWarning(ex);
                        success = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (success)
                LogMessage?.LogWarning(ex);
            success = false;
        }

        #endregion //上传变量内存队列中的数据
    }



}
