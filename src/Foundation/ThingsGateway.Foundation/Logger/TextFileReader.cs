//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Buffers;
using System.Text;

using ThingsGateway.NewLife.Caching;

namespace ThingsGateway.Foundation;

/// <summary>
/// 日志数据
/// </summary>
public class LogData
{
    /// <summary>
    /// 异常
    /// </summary>
    public string? ExceptionString { get; set; }

    /// <summary>
    /// 级别
    /// </summary>
    public LogLevel LogLevel { get; set; }

    /// <summary>
    /// 时间
    /// </summary>
    public string LogTime { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; }
}


/// <summary>日志文本文件倒序读取</summary>

public class LogDataCache
{
    public List<LogData> LogDatas { get; set; }
    public long Length { get; set; }
}

/// <summary>高性能日志文件读取器（支持倒序读取）</summary>
public class TextFileReader
{
    private static readonly MemoryCache _cache = new() { Expire = 30 };
    private static readonly MemoryCache _fileLocks = new();
    private static readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;
    /// <summary>
    /// 获取指定目录下所有文件信息
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <returns>包含文件信息的列表</returns>
    public static OperResult<List<string>> GetFiles(string directoryPath)
    {
        OperResult<List<string>> result = new(); // 初始化结果对象
        // 检查目录是否存在
        if (!Directory.Exists(directoryPath))
        {
            result.OperCode = 999;
            result.ErrorMessage = "Directory not exists";
            return result;
        }

        // 获取目录下所有文件路径
        var files = Directory.GetFiles(directoryPath);

        // 如果文件列表为空，则返回空列表
        if (files == null || files.Length == 0)
        {
            result.OperCode = 999;
            result.ErrorMessage = "Canot found files";
            return result;
        }

        // 获取文件信息并按照最后写入时间降序排序
        var fileInfos = files.Select(filePath => new FileInfo(filePath))
                             .OrderByDescending(x => x.LastWriteTime)
                             .Select(x => x.FullName)
                             .ToList();
        result.OperCode = 0;
        result.Content = fileInfos;
        return result;
    }


    public static OperResult<List<LogData>> LastLog(string file, int lineCount = 200)
    {
        if (!File.Exists(file))
            return new OperResult<List<LogData>>("The file path is invalid");

        _fileLocks.SetExpire(file, TimeSpan.FromSeconds(30));
        var fileLock = _fileLocks.GetOrAdd(file, _ => new object());
        lock (fileLock)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                var length = fileInfo.Length;
                var cacheKey = $"{nameof(TextFileReader)}_{nameof(LastLog)}_{file})";
                if (_cache.TryGetValue<LogDataCache>(cacheKey, out var cachedData))
                {
                    if (cachedData != null && cachedData.Length == length)
                    {
                        return new OperResult<List<LogData>>() { Content = cachedData.LogDatas };
                    }
                    else
                    {
                        _cache.Remove(cacheKey);
                    }
                }

                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                var result = ReadLogsInverse(fs, lineCount, fileInfo.Length);

                _cache.Set(cacheKey, new LogDataCache
                {
                    LogDatas = result,
                    Length = fileInfo.Length,
                });

                return new OperResult<List<LogData>>() { Content = result };
            }
            catch (Exception ex)
            {
                return new OperResult<List<LogData>>(ex);
            }
        }
    }

    private static List<LogData> ReadLogsInverse(FileStream fs, int lineCount, long length)
    {
        length = fs.Length;
        long ps = 0; // 保存起始位置
        List<string> txt = new(); // 存储读取的文本内容

        if (ps <= 0) // 如果起始位置小于等于0，将起始位置设置为文件长度
            ps = length - 1;

        // 循环读取指定行数的文本内容
        for (int i = 0; i < lineCount; i++)
        {
            ps = InverseReadRow(fs, ps, out var value); // 使用逆序读取
            txt.Add(value);
            if (ps <= 0) // 如果已经读取到文件开头则跳出循环
                break;
        }

        // 使用单次 LINQ 操作进行过滤和解析
        var result = txt
              .Select(a => ParseCSV(a))
                  .Where(data => data.Count >= 3)
                  .Select(data =>
                  {
                      var log = new LogData
                      {
                          LogTime = data[0].Trim(),
                          LogLevel = Enum.TryParse(data[1].Trim(), out LogLevel level) ? level : LogLevel.Info,
                          Message = data[2].Trim(),
                          ExceptionString = data.Count > 3 ? data[3].Trim() : null
                      };
                      return log;
                  })
                  .ToList();


        return result; // 返回解析结果
    }

    private static long InverseReadRow(FileStream fs, long position, out string value, int maxRead = 102400)
    {
        byte n = 0xD;
        byte a = 0xA;
        value = string.Empty;

        if (fs.Length == 0) return 0;

        var newPos = position;
        byte[] buffer = _bytePool.Rent(maxRead); // 从池中租借字节数组
        int index = 0;

        try
        {
            while (true)
            {
                if (newPos <= 0)
                    newPos = 0;

                fs.Position = newPos;
                int byteRead = fs.ReadByte();

                if (byteRead == -1) break;

                if (index >= maxRead)
                {
                    newPos = -1;
                    return newPos;
                }

                buffer[index++] = (byte)byteRead;

                if (byteRead == n || byteRead == a)
                {
                    if (MatchSeparator(buffer, index))
                    {
                        index -= TextFileLogger.SeparatorBytes.Length;
                        break;
                    }
                }

                newPos--;
                if (newPos <= -1)
                    break;
            }

            if (index >= 10)
            {
                Array.Reverse(buffer, 0, index); // 倒序
                value = Encoding.UTF8.GetString(buffer, 0, index);
            }

            return newPos;
        }
        finally
        {
            _bytePool.Return(buffer); // 归还数组
        }
    }


    private static bool MatchSeparator(byte[] arr, int length)
    {
        if (length < TextFileLogger.SeparatorBytes.Length)
            return false;

        int pos = length - 1;
        for (int i = 0; i < TextFileLogger.SeparatorBytes.Length; i++)
        {
            if (arr[pos] != TextFileLogger.SeparatorBytes[i])
                return false;
            pos--;
        }
        return true;
    }


    private static List<string> ParseCSV(string data)
    {
        List<string> items = new List<string>();

        int i = 0;
        while (i < data.Length)
        {
            // 当前字符不是逗号，开始解析一个新的数据项
            if (data[i] != ',')
            {
                int j = i;
                bool inQuotes = false;

                // 解析到一个未闭合的双引号时，继续读取下一个数据项
                while (j < data.Length && (inQuotes || data[j] != ','))
                {
                    if (data[j] == '\"')
                    {
                        inQuotes = !inQuotes;
                    }
                    j++;
                }

                // 去掉前后的双引号并将当前数据项加入列表中
                items.Add(RemoveQuotes(data.Substring(i, j - i)));

                // 跳过当前数据项结尾的逗号
                if (j < data.Length && data[j] == ',')
                {
                    j++;
                }

                i = j;
            }
            // 当前字符是逗号，跳过它
            else
            {
                i++;
            }
        }

        return items;
    }

    private static string RemoveQuotes(string data)
    {
        if (data.Length >= 2 && data[0] == '\"' && data[data.Length - 1] == '\"')
        {
            return data.Substring(1, data.Length - 2);
        }
        else
        {
            return data;
        }
    }
}
