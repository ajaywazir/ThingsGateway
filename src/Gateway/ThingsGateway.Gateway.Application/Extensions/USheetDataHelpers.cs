//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------


namespace ThingsGateway.Gateway.Application
{
    internal static class USheetDataHelpers
    {

        public static USheetDatas GetUSheetDatas(Dictionary<string, IList<Dictionary<string, object>>> data)
        {
            var uSheetDatas = new USheetDatas();

            foreach (var a in data)
            {
                var uSheetData = new USheetData();
                uSheetData.id = a.Key;
                uSheetData.name = a.Key;

                for (int row1 = 0; row1 < a.Value.Count; row1++)
                {
                    if (row1 == 0)
                    {
                        Dictionary<int, USheetCelldata> usheetColldata = new();
                        int col = 0;
                        foreach (var colData in a.Value[row1])
                        {
                            usheetColldata.Add(col, new USheetCelldata() { v = colData.Key });
                            col++;
                        }
                        uSheetData.cellData.Add(row1, usheetColldata);
                    }
                    {

                        Dictionary<int, USheetCelldata> usheetColldata = new();
                        int col = 0;
                        foreach (var colData in a.Value[row1])
                        {
                            usheetColldata.Add(col, new USheetCelldata() { v = colData.Value });
                            col++;
                        }
                        uSheetData.cellData.Add(row1 + 1, usheetColldata);
                    }
                }
                uSheetDatas.sheets.Add(a.Key, uSheetData);
            }

            return uSheetDatas;
        }
    }
}