namespace Content.Server._Mriya.NPC.AI;

public static class AIKeys
{
    public const string TargetDead = "TargetDead";
    public const string WeaponLoaded = "WeaponLoaded";
    public const string WeaponEquipped = "WeaponEquipped";
    public const string WeaponWielded = "WeaponWielded";
    public const string MeleeArmed = "MeleeArmed";
    public const string InRange = "InRange";
    public const string InMeleeZone = "InMeleeZone";
    public const string GrenadeAvailable = "GrenadeAvailable";
    public const string TargetVisible = "TargetVisible";
    public const string AtCover = "AtCover";
    public const string AtAmbushNode = "AtAmbushNode";
    public const string Dodged = "Dodged";
    public const string Suppressed = "Suppressed";
    public const string TargetFlushed = "TargetFlushed";
    public const string TargetSearched = "TargetSearched";
    public const string SafeFromExplosion = "SafeFromExplosion";
    public const string Inspected = "Inspected";
    public const string PatrolActive = "PatrolActive";
}

public static class AIValues
{
    public const string True = "True";
    public const string False = "False";

    public static string From(bool value) => value ? True : False;
}
