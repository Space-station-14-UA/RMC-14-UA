namespace Content.Shared._Mriya.RoundEnd;

public readonly record struct PlayerPlantedWeedsEvent(EntityUid User, int Amount = 1);

public readonly record struct PlayerBuiltBarricadeEvent(EntityUid User, int Amount = 1);
