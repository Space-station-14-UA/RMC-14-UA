using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;


namespace Content.Shared.Mriya.Sponsors;

/// <summary>
/// Network message containing sponsor information. Used to transmit the list of sponsor tags from the server to the client.
/// </summary>
public sealed class MsgSponsorInfo : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public List<string> Tags = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadInt32();
        Tags = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            Tags.Add(buffer.ReadString());
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Tags.Count);
        foreach (var tag in Tags)
        {
            buffer.Write(tag);
        }
    }
}
