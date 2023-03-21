﻿using Microsoft.Extensions.Logging;

using ThingsGateway.Foundation;
using ThingsGateway.Foundation.Extension;

namespace ThingsGateway.Siemens
{
    internal static class S7Helper
    {
        internal static OperResult<List<DeviceVariableSourceRead>> LoadSourceRead(this List<CollectVariableRunTime> deviceVariables, ILogger _logger, IThingsGatewayBitConverter byteConverter, SiemensS7PLC siemensS7Net)
        {
            var result = new List<DeviceVariableSourceRead>();
            try
            {
                //需要先剔除额外信息，比如dataformat等
                foreach (var item in deviceVariables)
                {
                    var address = item.VariableAddress;

                    IThingsGatewayBitConverter transformParameter = ByteConverterHelper.GetTransByAddress(
                     ref address, byteConverter, out int length, out BcdFormat bCDFormat);
                    item.ThingsGatewayBitConverter = transformParameter;
                    item.StringLength = length;
                    item.StringBcdFormat = bCDFormat;
                    item.VariableAddress = address;

                    int bitIndex = 0;
                    string[] addressSplits = new string[] { address };
                    if (address.IndexOf('.') > 0)
                    {
                        addressSplits = address.SplitDot();
                        try
                        {
                            if ((addressSplits.Length == 2 && !address.ToUpper().Contains("DB")) || (addressSplits.Length >= 3 && address.ToUpper().Contains("DB")))
                                bitIndex = Convert.ToInt32(addressSplits.Last());

                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "自动分包方法获取Bit失败");
                        }
                    }
                    item.Index = bitIndex;
                }
                //按读取间隔分组
                var tags = deviceVariables.GroupBy(it => it.IntervalTime);
                foreach (var item in tags)
                {
                    Dictionary<SiemensAddress, CollectVariableRunTime> map = item.ToDictionary(it =>
                    {

                        var lastLen = it.DataTypeEnum.GetByteLength(); ;
                        if (lastLen <= 0)
                        {
                            if (it.DataTypeEnum.GetNetType() == typeof(bool))
                            {
                                lastLen = 2;
                            }
                            else if (it.DataTypeEnum.GetNetType() == typeof(string))
                            {
                                lastLen = it.StringLength;
                            }
                            else if (it.DataTypeEnum.GetNetType() == typeof(object))
                            {
                                lastLen = 1;
                            }
                        }

                        var s7Address = SiemensAddress.ParseFrom(it.VariableAddress);
                        if (s7Address.IsSuccess)
                        {
                            if ((s7Address.Content.DataCode == (byte)S7WordLength.Counter || s7Address.Content.DataCode == (byte)S7WordLength.Timer) && lastLen == 1)
                            {
                                lastLen = 2;
                            }
                        }
                        //这里把每个变量的应读取长度都写入变量地址实体中
                        return SiemensAddress.ParseFrom(it.VariableAddress, (ushort)lastLen).Content;

                    });

                    //获取变量的地址
                    var modbusAddressList = map.Keys.ToList();

                    //获取S7数据代码
                    var functionCodes = modbusAddressList.Select(t => t.DataCode).Distinct();
                    foreach (var functionCode in functionCodes)
                    {
                        //相同数据代码的变量集合
                        var modbusAddressSameFunList = modbusAddressList
                            .Where(t => t.DataCode == functionCode);
                        //相同数据代码的变量集合中的不同DB块
                        var stationNumbers = modbusAddressSameFunList
                            .Select(t => t.DbBlock).Distinct();
                        foreach (var stationNumber in stationNumbers)
                        {
                            var addressList = modbusAddressSameFunList.Where(t => t.DbBlock == stationNumber)
                                .ToDictionary(t => t, t => map[t]);
                            //循环对数据代码，站号都一样的变量进行分配连读包
                            var tempResult = LoadSourceRead(addressList, functionCode, item.Key, siemensS7Net);
                            //添加到总连读包
                            result.AddRange(tempResult.Content);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "自动分包失败");
            }
            return OperResult.CreateSuccessResult(result);
        }

        private static OperResult<List<DeviceVariableSourceRead>> LoadSourceRead(Dictionary<SiemensAddress, CollectVariableRunTime> addressList, int functionCode, int timeInterval, SiemensS7PLC siemensS7Net)
        {

            List<DeviceVariableSourceRead> sourceReads = new List<DeviceVariableSourceRead>();
            //实际地址与长度排序
            var addresss = addressList.Keys.OrderBy(it =>
            {
                int address = 0;
                if (it.DataCode == (byte)S7WordLength.Counter || it.DataCode == (byte)S7WordLength.Timer)
                {
                    address = it.AddressStart * 2;
                }
                else
                {
                    address = it.AddressStart / 8;
                }
                return address + it.Length;
            }).ToList();
            var minAddress = addresss.First().AddressStart;
            var maxAddress = addresss.Last().AddressStart;
            while (maxAddress >= minAddress)
            {
                //这里直接避免末位变量长度超限的情况，pdu长度-8
                int readLength = siemensS7Net.PDULength == 0 ? 200 : siemensS7Net.PDULength - 8;
                List<SiemensAddress> tempAddress = new();
                if (functionCode == (byte)S7WordLength.Counter || functionCode == (byte)S7WordLength.Timer)
                {
                    tempAddress = addresss.Where(t => t.AddressStart >= minAddress && ((t.AddressStart) + t.Length) <= ((minAddress) + readLength)).ToList();
                    while ((tempAddress.Last().AddressStart * 2) + tempAddress.Last().Length - (tempAddress.First().AddressStart * 2) > readLength)
                    {
                        tempAddress.Remove(tempAddress.Last());
                    }
                }
                else
                {
                    tempAddress = addresss.Where(t => t.AddressStart >= minAddress && ((t.AddressStart) + t.Length) <= ((minAddress) + readLength)).ToList();
                    while ((tempAddress.Last().AddressStart / 8) + tempAddress.Last().Length - (tempAddress.First().AddressStart / 8) > readLength)
                    {
                        tempAddress.Remove(tempAddress.Last());
                    }
                }

                //读取寄存器长度
                int lastAddress = 0;
                int firstAddress = 0;
                if (functionCode == (byte)S7WordLength.Counter || functionCode == (byte)S7WordLength.Timer)
                {
                    lastAddress = tempAddress.Last().AddressStart * 2;
                    firstAddress = tempAddress.First().AddressStart * 2;
                }
                else
                {
                    lastAddress = tempAddress.Last().AddressStart / 8;
                    firstAddress = tempAddress.First().AddressStart / 8;
                }
                var sourceLen = lastAddress + tempAddress.Last().Length - firstAddress;
                DeviceVariableSourceRead sourceRead = new DeviceVariableSourceRead(timeInterval);
                sourceRead.Address = tempAddress.OrderBy(it => it.AddressStart).First().ToString();
                sourceRead.Length = sourceLen.ToString();
                foreach (var item in tempAddress)
                {
                    var readNode = addressList[item];
                    if (functionCode == (byte)S7WordLength.Counter || functionCode == (byte)S7WordLength.Timer)
                    {
                        if (readNode.DataTypeEnum == DataTypeEnum.Bool)
                        {
                            readNode.Index = (((item.AddressStart * 2) - (tempAddress.First().AddressStart * 2)) * 8) + readNode.Index;
                        }
                        else
                        {
                            readNode.Index = (item.AddressStart * 2) - (tempAddress.First().AddressStart * 2) + readNode.Index;
                        }
                    }
                    else
                    {
                        if (readNode.DataTypeEnum == DataTypeEnum.Bool)
                        {
                            readNode.Index = (((item.AddressStart / 8) - (tempAddress.First().AddressStart / 8)) * 8) + readNode.Index;
                        }
                        else
                        {
                            readNode.Index = (item.AddressStart / 8) - (tempAddress.First().AddressStart / 8) + readNode.Index;
                        }
                    }
                    sourceRead.DeviceVariables.Add(readNode);
                    addresss.Remove(item);
                }
                sourceReads.Add(sourceRead);
                if (addresss.Count > 0)
                    minAddress = addresss.First().AddressStart;
                else
                    break;
            }
            return OperResult.CreateSuccessResult(sourceReads);
        }

    }
}