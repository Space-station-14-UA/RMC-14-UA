using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Spawners.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Mriya.Hunter.Systems;

public sealed class HunterSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        var readyPlayers = _gameTicker.ReadyPlayerCount();
        var totalPlayers = _playerManager.PlayerCount;
        var playerCount = Math.Max(readyPlayers, totalPlayers);

        // Шанс 10% за умови, що гравців більше 20
        bool shouldSpawn = playerCount > 20 && _random.Prob(0.10f);

        var spawnPoints = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var hunterSpawns = new List<EntityCoordinates>();

        while (spawnPoints.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (spawnPoint.Job == "MRHunter")
            {
                hunterSpawns.Add(xform.Coordinates);
                // Завжди видаляємо стандартний маркер спавну, щоб роль контролювалася системою
                QueueDel(uid);
            }
        }

        if (shouldSpawn && hunterSpawns.Count > 0)
        {
            var spawnCoords = _random.Pick(hunterSpawns);
            Spawn("CMMobHunterGhostRole", spawnCoords);
            Log.Info($"Hunter ghost role spawned for round! Players: {playerCount}");
        }
        else
        {
            Log.Info($"Hunter ghost role skipped for round. Players: {playerCount}, Spawned: {shouldSpawn}");
        }
    }
}
