namespace Content.Server.Mriya.Sponsors;

/// <summary>
/// Message regarding setting the ghost color. Used to change the ghost color on the client side.
/// </summary>
public sealed class SetGhostColorMsg
{
    public Color Color { get; set; }
}
