namespace Content.Server._Mriya.NPC.AI;

public static class AIDefaults
{
    public static List<AIAction> CreateActions()
    {
        return new List<AIAction>
        {
            new AIEquipWeaponAction(),
            new AIWieldWeaponAction(),
            new AIDrawKnifeAction(),
            new AIAdvanceAction(),
            new AICloseToMeleeAction(),
            new AIAttackAction(),
            new AIMeleeAction(),
            new AISuppressionFireAction(),
            new AIBlindFireAction(),
            new AIGrenadeAction(),

            new AIReloadAction(),

            new AIInspectAction(),
            new AISearchLastKnownAction(),
            new AIPatrolAction(),

            new AIMoveToCoverAction(),
            new AIAmbushAction(),
            new AIDodgeAction(),
            new AIFleeExplosionAction(),
        };
    }

    public static List<AIGoal> CreateGoals()
    {
        return new List<AIGoal>
        {
            new AIKillEnemyGoal(),
            new AIDodgeGoal(),
            new AICoverGoal(),
            new AIAmbushGoal(),
            new AISuppressGoal(),
            new AIFlushGoal(),
            new AISearchGoal(),
            new AIFleeExplosionGoal(),
            new AIPatrolGoal(),
            new AIInspectGoal(),
        };
    }
}
