using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

[Serializable, NetSerializable]
public enum ContactHudPhase : byte
{
    None,
    Staging,
    Deployed,
    Ended,
}

/// <summary>
/// Broadcast by the Contact rule on every change and periodically
/// Times are server CurTime; the client counts down locally
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactHudStateEvent(ContactHudPhase phase, TimeSpan? fieldEndsAt, TimeSpan? nextWaveAt, int wave)
    : EntityEventArgs
{
    public readonly ContactHudPhase Phase = phase;
    public readonly TimeSpan? FieldEndsAt = fieldEndsAt;
    public readonly TimeSpan? NextWaveAt = nextWaveAt;
    public readonly int Wave = wave;
}

/// <summary>
/// Sent to one player when their є-бали change
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactBalanceEvent(int balance) : EntityEventArgs
{
    public readonly int Balance = balance;
}