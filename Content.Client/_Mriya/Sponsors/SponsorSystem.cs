using Content.Shared.Mriya.Sponsors;
using Robust.Shared.Network;

namespace Content.Client.Mriya.Sponsors;

/// <summary>
/// Interface for managing sponsors on the client side.
/// </summary>
public interface IClientSponsorManager
{
    void Initialize();
    bool HasTag(string tag);
}

/// <summary>
/// Client-side sponsor manager. Stores cached sponsor tags and provides methods for verifying tag existence.
/// </summary>
public sealed partial class ClientSponsorManager : IClientSponsorManager
{
    [Dependency] private INetManager _net = default!;

    private readonly HashSet<string> _tags = new();

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgSponsorInfo>(HandleSponsorInfo);
    }

    /// <summary>
    /// Updates the sponsor tags cache based on the received <see cref="MsgSponsorInfo"/> message.
    /// </summary>
    /// <param name="msg">The sponsor information update message.</param>
    private void HandleSponsorInfo(MsgSponsorInfo msg)
    {
        _tags.Clear();
        foreach (var tag in msg.Tags)
        {
            _tags.Add(tag);
        }
    }

    /// <summary>
    /// Checks whether the sponsor has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check.</param>
    /// <returns><see langword="true"/> if the sponsor has the tag; otherwise, <see langword="false"/>.</returns>
    public bool HasTag(string tag)
    {
        return _tags.Contains(tag);
    }
}
