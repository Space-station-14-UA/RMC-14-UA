using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Terminal;

[Serializable, NetSerializable]
public sealed class MriyaTerminalAdminEuiState : EuiStateBase
{
    public readonly List<string> Messages;

    public MriyaTerminalAdminEuiState(List<string> messages)
    {
        Messages = messages;
    }
}

public static class MriyaTerminalAdminEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Send : EuiMessageBase
    {
        public readonly string Message;

        public Send(string message)
        {
            Message = message;
        }
    }
}
