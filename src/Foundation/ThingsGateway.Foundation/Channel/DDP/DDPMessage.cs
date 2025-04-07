//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using TouchSocket.Core;

namespace ThingsGateway.Foundation;

/// <summary>
/// <inheritdoc/>
/// </summary>
public abstract class DDPMessage : MessageBase, IResultMessage
{
    /// <inheritdoc/>
    public override int HeaderLength => 4;
    public byte Type = 0;
    public string Id;
    public override FilterResult CheckBody<TByteBlock>(ref TByteBlock byteBlock)
    {
        Id = byteBlock.ToString(byteBlock.Position, 11).Replace("\0", "");
        OperCode = 0;
        Content = byteBlock.Span.Slice(byteBlock.Position + 11, BodyLength - 12).ToArray();
        return FilterResult.Success;
    }

    public override bool CheckHead<TByteBlock>(ref TByteBlock byteBlock)
    {
        var code = byteBlock.ReadByte();
        Type = byteBlock.ReadByte();

        if (code != 0x7B)
        {
            return false;
        }
        else
        {
            BodyLength = byteBlock.ReadUInt16(EndianType.Big) - 4;
            return true;
        }

    }

    public abstract int GetBodyLength<TByteBlock>(ref TByteBlock byteBlock) where TByteBlock : IByteBlock;

}


public class DDPTcpMessage : DDPMessage
{
    public override int GetBodyLength<TByteBlock>(ref TByteBlock byteBlock)
    {
        return byteBlock.ReadUInt16(EndianType.Big) - 4;
    }
}

public class DDPUdpMessage : DDPMessage
{
    public override int GetBodyLength<TByteBlock>(ref TByteBlock byteBlock)
    {
        return byteBlock.Length - 4;
    }
}