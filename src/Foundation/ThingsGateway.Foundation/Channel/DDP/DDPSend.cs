//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Text;

namespace ThingsGateway.Foundation;

/// <summary>
/// <inheritdoc/>
/// </summary>
public class DDPSend : ISendMessage
{

    public int MaxLength => 300;
    public int Sign { get; set; }
    ReadOnlyMemory<byte> ReadOnlyMemory;
    string Id;
    byte Command;
    bool Tcp;
    public DDPSend(ReadOnlyMemory<byte> readOnlyMemory, string id, bool tcp, byte command = 0x89)
    {
        Tcp = tcp;
        ReadOnlyMemory = readOnlyMemory;
        Id = id;
        Command = command;
    }
    public void Build<TByteBlock>(ref TByteBlock byteBlock) where TByteBlock : IByteBlock
    {
        byteBlock.WriteByte(0x7b);
        byteBlock.WriteByte(Command);
        byteBlock.WriteUInt16(0x10, EndianType.Big);//len
        byteBlock.Write(PadTo11Byte(Id.Remove(0, 3)));
        if (Tcp)
        {
            byteBlock.Write(ReadOnlyMemory.Span);
            byteBlock.WriteByte(0x7b);
            byteBlock.Position = 2;
            byteBlock.WriteUInt16((ushort)byteBlock.Length, EndianType.Big);//len
        }
        else
        {
            byteBlock.WriteByte(0x7b);
            byteBlock.Write(ReadOnlyMemory.Span);
        }
    }

    private static byte[] PadTo11Byte(string id)
    {
        var bytes = Encoding.UTF8.GetBytes(id);

        if (bytes.Length < 11)
        {
            byte[] newBytes = new byte[11];
            Array.Copy(bytes, newBytes, bytes.Length);
            for (int i = bytes.Length; i < 11; i++)
            {
                newBytes[i] = 0;
            }
            return newBytes;
        }
        return bytes;

    }
}
