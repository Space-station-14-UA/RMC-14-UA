using Content.Shared._Mriya.Rules.Contact;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Keeps the latest Contact HUD state from the server and shows the overlay while a round is on.
/// </summary>
public sealed class ContactHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public ContactHudPhase Phase { get; private set; }
    public TimeSpan? FieldEndsAt { get; private set; }
    public bool FieldUnavailable { get; private set; }
    public TimeSpan? NextWaveAt { get; private set; }
    public int Wave { get; private set; }
    public int? Balance { get; private set; }

    public ContactShuttleState? ShuttleState { get; private set; }
    public TimeSpan? ShuttleEndsAt { get; private set; }

    public ContactExtractionState ExtractionState { get; private set; }
    public List<NetCoordinates> ExtractionZones { get; private set; } = new();
    public float ExtractionRadius { get; private set; }
    public TimeSpan? ExtractionEndsAt { get; private set; }
    public int ExtractionInZone { get; private set; }
    public int ExtractionNeeded { get; private set; }

    private ContactHudOverlay? _overlay;

    public override void Initialize()
    {
        SubscribeNetworkEvent<ContactHudStateEvent>(OnState);
        SubscribeNetworkEvent<ContactBalanceEvent>(OnBalance);
        SubscribeNetworkEvent<ContactShuttleHudEvent>(OnShuttle);
        SubscribeNetworkEvent<ContactExtractionHudEvent>(OnExtraction);
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
        FieldUnavailable = ev.FieldUnavailable;
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

    private void OnShuttle(ContactShuttleHudEvent ev)
    {
        ShuttleState = ev.State;
        ShuttleEndsAt = ev.EndsAt;
    }

    private void OnExtraction(ContactExtractionHudEvent ev)
    {
        ExtractionState = ev.State;
        ExtractionZones = ev.Zones;
        ExtractionRadius = ev.Radius;
        ExtractionEndsAt = ev.EndsAt;
        ExtractionInZone = ev.InZone;
        ExtractionNeeded = ev.Needed;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        Phase = ContactHudPhase.None;
        FieldEndsAt = null;
        FieldUnavailable = false;
        NextWaveAt = null;
        Wave = 0;
        Balance = null;
        ShuttleState = null;
        ShuttleEndsAt = null;
        ExtractionState = ContactExtractionState.None;
        ExtractionZones = new List<NetCoordinates>();
        ExtractionEndsAt = null;
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