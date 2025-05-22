//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Collections;

namespace ThingsGateway.Gateway.Application;

public class ThreadSafeStringDictionary<T> : IDictionary<string, T>, IReadOnlyDictionary<string, T>
{
    private const int DEFAULT_PARTITIONS = 128;
    private readonly Dictionary<string, T>[] _partitions;
    private readonly object[] _partitionLocks;
    private readonly IEqualityComparer<string> _comparer;

    public ThreadSafeStringDictionary() : this(DEFAULT_PARTITIONS, null) { }

    public ThreadSafeStringDictionary(int partitionCount, IEqualityComparer<string> comparer)
    {
        if (partitionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(partitionCount));

        _partitions = new Dictionary<string, T>[partitionCount];
        _partitionLocks = new object[partitionCount];
        _comparer = comparer ?? StringComparer.Ordinal;

        for (int i = 0; i < partitionCount; i++)
        {
            _partitions[i] = new Dictionary<string, T>(_comparer);
            _partitionLocks[i] = new object();
        }
    }

    private int GetPartitionIndex(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return Math.Abs(_comparer.GetHashCode(key)) % _partitions.Length;
    }

    // 基本操作
    public T this[string key]
    {
        get
        {
            int index = GetPartitionIndex(key);
            lock (_partitionLocks[index])
            {
                return _partitions[index][key];
            }
        }
        set
        {
            int index = GetPartitionIndex(key);
            lock (_partitionLocks[index])
            {
                _partitions[index][key] = value;
            }
        }
    }

    public ICollection<string> Keys => GetAllItems().Select(kv => kv.Key).ToList();
    public ICollection<T> Values => GetAllItems().Select(kv => kv.Value).ToList();

    public int Count
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _partitions.Length; i++)
            {
                lock (_partitionLocks[i])
                {
                    count += _partitions[i].Count;
                }
            }
            return count;
        }
    }

    public bool IsReadOnly => false;

    IEnumerable<string> IReadOnlyDictionary<string, T>.Keys => Keys;

    IEnumerable<T> IReadOnlyDictionary<string, T>.Values => Values;

    public void Add(string key, T value)
    {
        int index = GetPartitionIndex(key);
        lock (_partitionLocks[index])
        {
            _partitions[index].Add(key, value);
        }
    }

    public void Add(KeyValuePair<string, T> item) => Add(item.Key, item.Value);

    public void Clear()
    {
        for (int i = 0; i < _partitions.Length; i++)
        {
            lock (_partitionLocks[i])
            {
                _partitions[i].Clear();
            }
        }
    }

    public bool Contains(KeyValuePair<string, T> item)
    {
        int index = GetPartitionIndex(item.Key);
        lock (_partitionLocks[index])
        {
            return _partitions[index].TryGetValue(item.Key, out var value) &&
                   EqualityComparer<T>.Default.Equals(value, item.Value);
        }
    }

    public bool ContainsKey(string key)
    {
        int index = GetPartitionIndex(key);
        lock (_partitionLocks[index])
        {
            return _partitions[index].ContainsKey(key);
        }
    }

    public bool Remove(string key)
    {
        int index = GetPartitionIndex(key);
        lock (_partitionLocks[index])
        {
            return _partitions[index].Remove(key);
        }
    }

    public bool Remove(KeyValuePair<string, T> item)
    {
        int index = GetPartitionIndex(item.Key);
        lock (_partitionLocks[index])
        {
            if (_partitions[index].TryGetValue(item.Key, out var value) &&
                EqualityComparer<T>.Default.Equals(value, item.Value))
            {
                return _partitions[index].Remove(item.Key);
            }
            return false;
        }
    }

    public bool TryGetValue(string key, out T value)
    {
        int index = GetPartitionIndex(key);
        lock (_partitionLocks[index])
        {
            return _partitions[index].TryGetValue(key, out value);
        }
    }

    public void CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count) throw new ArgumentException("Target array too small");

        foreach (var item in GetAllItems())
        {
            array[arrayIndex++] = item;
        }
    }

    // 枚举器实现
    public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
    {
        return GetAllItems().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void AddRange(IEnumerable<KeyValuePair<string, T>> items)
    {
        var grouped = items.GroupBy(item => GetPartitionIndex(item.Key));

        foreach (var group in grouped)
        {
            lock (_partitionLocks[group.Key])
            {
                foreach (var item in group)
                {
                    _partitions[group.Key][item.Key] = item.Value;
                }
            }
        }
    }

    public Dictionary<string, T> GetSnapshot()
    {
        var snapshot = new Dictionary<string, T>(_comparer);

        for (int i = 0; i < _partitions.Length; i++)
        {
            lock (_partitionLocks[i])
            {
                foreach (var kvp in _partitions[i])
                {
                    snapshot[kvp.Key] = kvp.Value;
                }
            }
        }

        return snapshot;
    }

    private IEnumerable<KeyValuePair<string, T>> GetAllItems()
    {
        for (int i = 0; i < _partitions.Length; i++)
        {
            lock (_partitionLocks[i])
            {
                foreach (var item in _partitions[i]) // 直接枚举原字典
                {
                    yield return item;
                }
            }
        }
    }
}

public class ThreadSafeLongDictionary<T> : IDictionary<long, T>, IReadOnlyDictionary<long, T>
{
    private const int DEFAULT_PARTITIONS = 128;
    private readonly Dictionary<long, T>[] _partitions;
    private readonly object[] _partitionLocks;

    public ThreadSafeLongDictionary() : this(DEFAULT_PARTITIONS) { }

    public ThreadSafeLongDictionary(int partitionCount)
    {
        if (partitionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(partitionCount));

        _partitions = new Dictionary<long, T>[partitionCount];
        _partitionLocks = new object[partitionCount];

        for (int i = 0; i < partitionCount; i++)
        {
            _partitions[i] = new Dictionary<long, T>();
            _partitionLocks[i] = new object();
        }
    }

    private int GetPartitionIndex(long key)
    {
        // 使用混合哈希算法减少碰撞
        uint hash = (uint)key;
        hash = ((hash >> 16) ^ hash) * 0x45d9f3b;
        hash = ((hash >> 16) ^ hash) * 0x45d9f3b;
        hash = (hash >> 16) ^ hash;
        return (int)(hash % _partitions.Length);
    }

    public T this[long key]
    {
        get
        {
            int index = GetPartitionIndex(key);
            lock (_partitionLocks[index])
            {
                return _partitions[index][key];
            }
        }
        set
        {
            int index = GetPartitionIndex(key);
            lock (_partitionLocks[index])
            {
                _partitions[index][key] = value;
            }
        }
    }

    public ICollection<long> Keys => GetAllItems().Select(kv => kv.Key).ToList();
    public ICollection<T> Values => GetAllItems().Select(kv => kv.Value).ToList();

    public int Count
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _partitions.Length; i++)
            {
                lock (_partitionLocks[i])
                {
                    count += _partitions[i].Count;
                }
            }
            return count;
        }
    }

    public bool IsReadOnly => false;

    IEnumerable<long> IReadOnlyDictionary<long, T>.Keys => Keys;

    IEnumerable<T> IReadOnlyDictionary<long, T>.Values => Values;

    public void Add(long key, T value)
    {
        int index = GetPartitionIndex(key);
        lock (_partitionLocks[index])
        {
            _partitions[index].Add(key, value);
        }
    }

    public void Add(KeyValuePair<long, T> item) => Add(item.Key, item.Value);

    public void Clear()
    {
        for (int i = 0; i < _partitions.Length; i++)
        {
            lock (_partitionLocks[i])
            {
                _partitions[i].Clear();
            }
        }
    }

    public bool Contains(KeyValuePair<long, T> item)
    {
        int index = GetPartitionIndex(item.Key);
        lock (_partitionLocks[index])
        {
            return _partitions[index].TryGetValue(item.Key, out var value) &&
                   EqualityComparer<T>.Default.Equals(value, item.Value);
        }
    }

    public bool ContainsKey(long key)
    {
        int index = GetPartitionIndex(key);
        lock (_partitionLocks[index])
        {
            return _partitions[index].ContainsKey(key);
        }
    }

    public bool Remove(long key)
    {
        int index = GetPartitionIndex(key);
        lock (_partitionLocks[index])
        {
            return _partitions[index].Remove(key);
        }
    }

    public bool Remove(KeyValuePair<long, T> item)
    {
        int index = GetPartitionIndex(item.Key);
        lock (_partitionLocks[index])
        {
            if (_partitions[index].TryGetValue(item.Key, out var value) &&
                EqualityComparer<T>.Default.Equals(value, item.Value))
            {
                return _partitions[index].Remove(item.Key);
            }
            return false;
        }
    }

    public bool TryGetValue(long key, out T value)
    {
        int index = GetPartitionIndex(key);
        lock (_partitionLocks[index])
        {
            return _partitions[index].TryGetValue(key, out value);
        }
    }

    public void CopyTo(KeyValuePair<long, T>[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count) throw new ArgumentException("Target array too small");

        foreach (var item in GetAllItems())
        {
            array[arrayIndex++] = item;
        }
    }

    public IEnumerator<KeyValuePair<long, T>> GetEnumerator()
    {
        return GetAllItems().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void AddRange(IEnumerable<KeyValuePair<long, T>> items)
    {
        var grouped = items.GroupBy(item => GetPartitionIndex(item.Key));

        foreach (var group in grouped)
        {
            lock (_partitionLocks[group.Key])
            {
                foreach (var item in group)
                {
                    _partitions[group.Key][item.Key] = item.Value;
                }
            }
        }
    }

    public Dictionary<long, T> GetSnapshot()
    {
        var snapshot = new Dictionary<long, T>();

        for (int i = 0; i < _partitions.Length; i++)
        {
            lock (_partitionLocks[i])
            {
                foreach (var kvp in _partitions[i])
                {
                    snapshot[kvp.Key] = kvp.Value;
                }
            }
        }

        return snapshot;
    }

    private IEnumerable<KeyValuePair<long, T>> GetAllItems()
    {
        for (int i = 0; i < _partitions.Length; i++)
        {
            lock (_partitionLocks[i])
            {
                foreach (var item in _partitions[i])
                {
                    yield return item;
                }
            }
        }
    }

    public string GetPartitionStats()
    {
        var stats = new System.Text.StringBuilder();
        for (int i = 0; i < _partitions.Length; i++)
        {
            lock (_partitionLocks[i])
            {
                stats.AppendLine($"Partition {i}: {_partitions[i].Count} items");
            }
        }
        return stats.ToString();
    }
}