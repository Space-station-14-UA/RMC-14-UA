using System.Numerics;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.NPC.AI;

[RegisterComponent]
public sealed partial class AIPlannerComponent : Component
{
    #region Configuration

    [DataField("enabled")]
    public bool Enabled = true;

    [DataField("useDefaultActions")]
    public bool UseDefaultActions = true;

    [DataField("useDefaultGoals")]
    public bool UseDefaultGoals = true;

    [DataField("actions")]
    public List<AIAction> Actions = new();

    [DataField("goals")]
    public List<AIGoal> Goals = new();

    [DataField("planCooldown")]
    public float PlanCooldown = 0.4f;

    [DataField("sensorInterval")]
    public float SensorInterval = 0.2f;

    [DataField("meleeZoneRadius")]
    public float MeleeZoneRadius = 1.5f;

    [DataField("rangedRange")]
    public float RangedRange = 12f;

    [DataField("visionRange")]
    public float VisionRange = 14f;

    [DataField("blindFireDuration")]
    public float BlindFireDuration = 2f;

    [DataField("suppressionDuration")]
    public float SuppressionDuration = 1.25f;

    [DataField("burstSize")]
    public int BurstSize = 3;

    [DataField("burstPause")]
    public float BurstPause = 0.85f;

    [DataField("burstPauseJitter")]
    public float BurstPauseJitter = 0.35f;

    [DataField("dodgeDuration")]
    public float DodgeDuration = 0.4f;

    [DataField("dodgeCooldown")]
    public float DodgeCooldown = 1.25f;

    [DataField("coverSearchDistance")]
    public float CoverSearchDistance = 4f;

    [DataField("ambushDistance")]
    public float AmbushDistance = 5f;

    [DataField("patrolRadius")]
    public float PatrolRadius = 6f;

    [DataField("coverCompromisedWindow")]
    public float CoverCompromisedWindow = 3f;

    [DataField("reloadDuration")]
    public float ReloadDuration = 1.5f;

    [DataField("squadRadius")]
    public float SquadRadius = 8f;

    [DataField("targetMemoryDuration")]
    public float TargetMemoryDuration = 12f;

    [DataField("patrolGroupRadius")]
    public float PatrolGroupRadius = 24f;

    [DataField("patrolMinGroupSize")]
    public int PatrolMinGroupSize = 3;

    [DataField("patrolMaxGroupSize")]
    public int PatrolMaxGroupSize = 5;

    [DataField("searchDuration")]
    public float SearchDuration = 6f;

    [DataField("flankCooldown")]
    public float FlankCooldown = 6f;

    [DataField("squadRoleDuration")]
    public float SquadRoleDuration = 9f;

    [DataField("squadGrenadeCooldown")]
    public float SquadGrenadeCooldown = 16f;

    [DataField("grenadeCooldown")]
    public float GrenadeCooldown = 16f;

    [DataField("grenadeThrowRange")]
    public float GrenadeThrowRange = 10f;

    [DataField("grenadeCoverWindow")]
    public float GrenadeCoverWindow = 6f;

    [DataField("grenadeDangerRadius")]
    public float GrenadeDangerRadius = 6f;

    [DataField("grenadeFriendlyRadius")]
    public float GrenadeFriendlyRadius = 3.5f;

    [DataField("hasGrenade")]
    public bool HasGrenade = false;

    #endregion

    #region Runtime state

    [ViewVariables]
    public AIState WorldState = new();

    [ViewVariables]
    public AIGoal? CurrentGoal;

    [ViewVariables]
    public List<AIAction>? Plan;

    [ViewVariables]
    public int PlanIndex;

    [ViewVariables]
    public bool ActionStarted;

    [ViewVariables]
    public bool NeedsReplan = true;

    [ViewVariables]
    public float PlanAccumulator;

    [ViewVariables]
    public float SensorAccumulator;

    [ViewVariables] public EntityUid Target;
    [ViewVariables] public bool HasTarget;
    [ViewVariables] public bool TargetVisible;
    [ViewVariables] public bool TargetAimingAtMe;
    [ViewVariables] public bool UnderFire;
    [ViewVariables] public bool CoverCompromised;
    [ViewVariables] public bool InCover;
    [ViewVariables] public bool WeaponLoaded;
    [ViewVariables] public bool WeaponEquipped;
    [ViewVariables] public bool WeaponWielded;
    [ViewVariables] public bool MeleeArmed;
    [ViewVariables] public bool InRange;
    [ViewVariables] public bool InMeleeZone;
    [ViewVariables] public bool GrenadeAvailable;
    [ViewVariables] public bool LowHealth;
    [ViewVariables] public bool HeardDisturbance;
    [ViewVariables] public float TargetDistance = float.MaxValue;
    [ViewVariables] public Vector2 LastKnownTargetPosition;
    [ViewVariables] public FixedPoint2 LastDamage;
    [ViewVariables] public TimeSpan CoverCompromisedUntil;
    [ViewVariables] public TimeSpan DodgeCooldownUntil;
    [ViewVariables] public TimeSpan BlindFireUntil;

    [ViewVariables] public EntityUid MemoryTarget;
    [ViewVariables] public TimeSpan LastSeenTime;
    [ViewVariables] public float LastSeenAge;
    [ViewVariables] public bool TargetInMemory;
    [ViewVariables] public bool TargetBehindCover;
    [ViewVariables] public TimeSpan FlankCooldownUntil;

    [ViewVariables] public bool GrenadeDanger;
    [ViewVariables] public Vector2 GrenadeDangerPosition;
    [ViewVariables] public TimeSpan GrenadeCooldownUntil;
    [ViewVariables] public TimeSpan SquadGrenadeCooldownUntil;

    [ViewVariables] public int SquadId = -1;
    [ViewVariables] public string SquadOrder = "None";
    [ViewVariables] public string SquadRole = "None";
    [ViewVariables] public bool SquadCanGrenade;

    [ViewVariables] public EntityUid PatrolLeader;
    [ViewVariables] public int PatrolSlot = -1;
    [ViewVariables] public int PatrolGroupSize;
    [ViewVariables] public float PatrolGroupSpread;

    #endregion
}
