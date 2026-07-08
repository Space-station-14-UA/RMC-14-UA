using Content.Client.Examine;
using Content.Shared.Preferences.Loadouts;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Content.Shared.Clothing;
using System.Numerics;
using Content.Shared.Input;

namespace Content.Client.Mriya.Sponsors.UI;

public sealed partial class SponsorLoadoutControl : PanelContainer
{
    [Dependency] private IEntityManager _entManager = default!;

    private readonly EntityUid? _dummyEntity;

    public SponsorLoadoutControl(LoadoutPrototype proto)
    {
        IoCManager.InjectDependencies(this);

        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#202020"),
            BorderThickness = new Thickness(1),
            BorderColor = Color.FromHex("#505050")
        };
        MinSize = new Vector2(64, 64);
        MouseFilter = MouseFilterMode.Stop;

        var spriteView = new SpriteView
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Scale = new Vector2(2, 2)
        };
        AddChild(spriteView);

        var loadoutSystem = _entManager.System<LoadoutSystem>();

        var entId = proto.DummyEntity ?? loadoutSystem.GetFirstOrNull(proto);

        if (entId != null)
        {
            _dummyEntity = _entManager.SpawnEntity(entId, MapCoordinates.Nullspace);
            spriteView.SetEntity(_dummyEntity);
            var meta = _entManager.GetComponent<MetaDataComponent>(_dummyEntity.Value);
            var itemName = loadoutSystem.GetName(proto);
            var itemDesc = meta.EntityDescription;

            var tooltipMsg = new FormattedMessage();
            tooltipMsg.AddMarkup($"[bold]{itemName}[/bold]");
            if (!string.IsNullOrEmpty(itemDesc))
            {
                tooltipMsg.PushNewline();
                tooltipMsg.AddText(itemDesc);
            }

            var tooltip = new Tooltip();
            tooltip.SetMessage(tooltipMsg);
            TooltipSupplier = _ => tooltip;
        }
        else
        {
            // Fallback, якщо сутності немає
            var tooltip = new Tooltip();
            tooltip.SetMessage(FormattedMessage.FromMarkup($"[bold]{loadoutSystem.GetName(proto)}[/bold]"));
            TooltipSupplier = _ => tooltip;
        }

        // Обробка Shift + Click для Examine (огляду)
        OnKeyBindDown += (args) =>
        {
            if (args.Function == ContentKeyFunctions.ExamineEntity && _dummyEntity != null)
            {
                var examineSystem = _entManager.System<ExamineSystem>();
                examineSystem.DoExamine(_dummyEntity.Value);
                args.Handle();
            }
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_dummyEntity != null && _entManager.EntityExists(_dummyEntity))
        {
            _entManager.DeleteEntity(_dummyEntity.Value);
        }
    }
}
