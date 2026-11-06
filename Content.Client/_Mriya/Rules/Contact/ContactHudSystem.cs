using Content.Shared._Mriya.Rules.Contact;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Keeps the latest Contact HUD state from the server and shows the overlay while a round is on.
/// </summary>
public sealed class ContactHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public ContactHudPhase Phase { get; private set; }
    public TimeSpan? FieldEndsAt { get; private set; }
    public TimeSpan? NextWaveAt { get; private set; }
    public int Wave { get; private set; }
    public int? Balance { get; private set; }

    private ContactHudOverlay? _overlay;

    public override void Initialize()
    {
        SubscribeNetworkEvent<ContactHudStateEvent>(OnState);
        SubscribeNetworkEvent<ContactBalanceEvent>(OnBalance);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        HideOverlay();
    }

    private void OnState(ContactHudStateEvent ev)
    {
        Phase = ev.Phase;
        FieldEndsAt = ev.FieldEndsAt;
        NextWaveAt = ev.NextWaveAt;
        Wave = ev.Wave;

        if (Phase is ContactHudPhase.Staging or ContactHudPhase.Deployed)
            ShowOverlay();
        else
            HideOverlay();
    }

    private void OnBalance(ContactBalanceEvent ev)
    {
        Balance = ev.Balance;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        Phase = ContactHudPhase.None;
        FieldEndsAt = null;
        NextWaveAt = null;
        Wave = 0;
        Balance = null;
        HideOverlay();
    }

    private void ShowOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new ContactHudOverlay(this);
        _overlays.AddOverlay(_overlay);
    }

    private void HideOverlay()
    {
        if (_overlay == null)
            return;

        _overlays.RemoveOverlay(_overlay);
        _overlay = null;
    }
}