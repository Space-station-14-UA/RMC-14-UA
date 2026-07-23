using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Mriya.Vehicle;

public sealed class MriyaVehicleNicknameSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MRVehicleNicknameComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, MRVehicleNicknameComponent comp, MapInitEvent args)
    {
        if (!TryComp<MetaDataComponent>(uid, out var meta))
            return;

        var dataset = _prototype.Index<LocalizedDatasetPrototype>(comp.Dataset);
        if (dataset.Values.Count == 0)
            return;

        var nickName = Loc.GetString(dataset.Values[_random.Next(dataset.Values.Count)]);

        var newName = $"{meta.EntityName} «{nickName}»";

        _metaData.SetEntityName(uid, newName, meta);
    }
}
