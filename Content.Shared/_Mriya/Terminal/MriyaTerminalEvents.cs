using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Terminal;

[Serializable, NetSerializable]
public enum MriyaTerminalUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class MriyaTerminalState : BoundUserInterfaceState
{
    public readonly bool IsInput;
    public readonly List<string> Messages;
    public readonly string? AuthorizedName;
    public readonly string? AuthorizedJob;

    public MriyaTerminalState(bool isInput, List<string> messages, string? authorizedName, string? authorizedJob)
    {
        IsInput = isInput;
        Messages = messages;
        AuthorizedName = authorizedName;
        AuthorizedJob = authorizedJob;
    }
}

[Serializable, NetSerializable]
public sealed class MriyaTerminalSendMessage : BoundUserInterfaceMessage
{
    public readonly string Message;

    public MriyaTerminalSendMessage(string message)
    {
        Message = message;
    }
}
