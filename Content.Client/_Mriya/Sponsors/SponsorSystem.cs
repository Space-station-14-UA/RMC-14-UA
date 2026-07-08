using Content.Shared.Mriya.Sponsors;
using Robust.Shared.Network;

namespace Content.Client.Mriya.Sponsors;
public sealed partial class SponsorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }
}

public interface IClientSponsorManager
{
    void Initialize();
    bool HasTag(string tag);
}

public sealed partial class ClientSponsorManager : IClientSponsorManager
{
    [Dependency] private INetManager _net = default!;

    private readonly HashSet<string> _tags = new();

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgSponsorInfo>(HandleSponsorInfo);
    }

    private void HandleSponsorInfo(MsgSponsorInfo msg)
    {
        _tags.Clear();
        foreach (var tag in msg.Tags)
        {
            _tags.Add(tag);
        }
    }

    public bool HasTag(string tag)
    {
        return _tags.Contains(tag);
    }
}
