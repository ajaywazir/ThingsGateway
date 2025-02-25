﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Text;

using ThingsGateway.Foundation.Extension.String;
using ThingsGateway.NewLife.Caching;
using ThingsGateway.NewLife.Extension;

namespace ThingsGateway.Foundation.Modbus;

/// <summary>
/// Modbus协议地址，字符串不包含长度与写入值、读写类型
/// </summary>
public class ModbusAddress : ModbusRequest
{
    public ModbusAddress()
    {
    }

    public ModbusAddress(ModbusRequest modbusAddress)
    {
        StartAddress = modbusAddress.StartAddress;
        Data = modbusAddress.Data;
        FunctionCode = modbusAddress.FunctionCode;
        Length = modbusAddress.Length;
        Station = modbusAddress.Station;
        StartAddress = modbusAddress.StartAddress;
    }

    public ModbusAddress(ModbusAddress modbusAddress)
    {
        StartAddress = modbusAddress.StartAddress;
        Data = modbusAddress.Data;
        FunctionCode = modbusAddress.FunctionCode;
        Length = modbusAddress.Length;
        BitIndex = modbusAddress.BitIndex;
        WriteFunctionCode = modbusAddress.WriteFunctionCode;
        Station = modbusAddress.Station;
        StartAddress = modbusAddress.StartAddress;
    }

    /// <summary>
    /// 读取终止，只用于打包
    /// </summary>
    public int AddressEnd => (ushort)(StartAddress + Math.Max(Math.Ceiling(Length / 2.0), 1));

    /// <summary>
    /// BitIndex
    /// </summary>
    public int? BitIndex { get; set; }

    /// <summary>
    /// 写入功能码，主站请求时生效，其余情况应选择<see cref="ModbusRequest.FunctionCode"/>
    /// </summary>
    public byte? WriteFunctionCode { get; set; }

    /// <summary>
    /// 解析地址
    /// </summary>
    public static ModbusAddress? ParseFrom(string address, byte? station = null, bool isCache = true)
    {
        if (string.IsNullOrWhiteSpace(address)) { return null; }
        var cacheKey = $"{nameof(ParseFrom)}_{typeof(ModbusAddress).FullName}_{typeof(ModbusAddress).TypeHandle.Value}_{station}_{address}";
        if (isCache)
            if (MemoryCache.Instance.TryGetValue(cacheKey, out ModbusAddress mAddress))
                return new(mAddress);

        var modbusAddress = new ModbusAddress();
        if (station != null)
            modbusAddress.Station = station.Value;

        if (address.IndexOf(';') < 0)
        {
            Address(address);
        }
        else
        {
            string[] strArray = address.SplitStringBySemicolon();
            for (int index = 0; index < strArray.Length; ++index)
            {
                if (strArray[index].StartsWith("S=", StringComparison.OrdinalIgnoreCase))
                {
                    if (strArray[index].Substring(2).ToInt() > 0)
                        modbusAddress.Station = byte.Parse(strArray[index].Substring(2));
                }
                else if (strArray[index].StartsWith("W=", StringComparison.OrdinalIgnoreCase))
                {
                    if (strArray[index].Substring(2).ToInt() > 0)
                        modbusAddress.WriteFunctionCode = byte.Parse(strArray[index].Substring(2));
                }
                else if (!strArray[index].Contains('='))
                {
                    Address(strArray[index]);
                }
            }
        }

        if (isCache)
            MemoryCache.Instance.Set(cacheKey, modbusAddress, 3600);

        return new(modbusAddress);

        void Address(string address)
        {
            var readF = ushort.Parse(address.Substring(0, 1));
            if (readF > 4)
                throw new(ModbusResource.Localizer["FunctionError"]);
            switch (readF)
            {
                case 0:
                    modbusAddress.FunctionCode = 1;
                    break;

                case 1:
                    modbusAddress.FunctionCode = 2;
                    break;

                case 3:
                    modbusAddress.FunctionCode = 4;
                    break;

                case 4:
                    modbusAddress.FunctionCode = 3;
                    break;
            }
            string[] strArray = address.SplitStringByDelimiter();
            modbusAddress.StartAddress = (ushort)(ushort.Parse(strArray[0].Substring(1)) - 1);
            if (strArray.Length > 1)
            {
                modbusAddress.BitIndex = int.Parse(strArray[1]);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder stringGeter = new();
        if (Station > 0)
        {
            stringGeter.Append($"S={Station};");
        }
        if (WriteFunctionCode > 0)
        {
            stringGeter.Append($"W={WriteFunctionCode};");
        }
        stringGeter.Append($"{GetFunctionString(FunctionCode)}{StartAddress + 1}{(BitIndex != null ? $".{BitIndex}" : null)}");
        return stringGeter.ToString();
    }

    private static string GetFunctionString(byte readF)
    {
        return readF switch
        {
            1 => "0",
            2 => "1",
            3 => "4",
            4 => "3",
            _ => "4",
        };
    }
}
