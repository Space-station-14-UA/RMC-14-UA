using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Mriya.Sponsors.UI;
using Content.Shared.Mriya.Sponsors;
using Robust.Shared.Player;

namespace Content.Server.Mriya.Sponsors;

public sealed partial class SponsorSystem : EntitySystem
{
    [Dependency] private EuiManager _euiManager = default!;
    [Dependency] private IAdminManager _adminManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestPersonalSponsorWindowMessage>(OnRequestPersonalWindow);
        SubscribeNetworkEvent<RequestAdminSponsorWindowMessage>(OnRequestAdminWindow);
        SubscribeNetworkEvent<RequestSponsorListWindowMessage>(OnRequestSponsorListWindow);
    }

    private void OnRequestPersonalWindow(RequestPersonalSponsorWindowMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } session)
            return;

        OpenPersonalEui(session);
    }

    private void OnRequestAdminWindow(RequestAdminSponsorWindowMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } session)
            return;

        if (!_adminManager.IsAdmin(session))
            return;

        OpenAdminEui(session);
    }

    private void OnRequestSponsorListWindow(RequestSponsorListWindowMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } session) return;

        OpenSponsorListEui(session);
    }

    public void OpenPersonalEui(ICommonSession session)
    {
        var eui = new PersonalSponsorEui();
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    public void OpenAdminEui(ICommonSession session)
    {
        var eui = new AdminSponsorsEui();
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    public void OpenSponsorListEui(ICommonSession session)
    {
        var eui = new SponsorListEui();
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }
}
