using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sich.Language;


[RegisterComponent]
public sealed partial class HunterLanguageComponent : Component
{

    [DataField("understandsHuman")]
    public bool UnderstandsHuman = true;


    [DataField("obfuscationPrefix")]
    public string ObfuscationPrefix = "cm-hunter-speech-obfuscated-";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HunterTranslatorComponent : Component
{

    [DataField("maxLength")]
    public int MaxLength = 200;

    [DataField("actionId")]
    public string ActionId = "HunterActionTranslator";

    [DataField("actionEntity")]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public HunterTranslationCategory TranslationTarget = HunterTranslationCategory.All;

    [DataField, AutoNetworkedField]
    public NetEntity? ActiveUser;
}

[Serializable, NetSerializable]
public enum HunterTranslationCategory : byte
{
    All = 0,
    Human,
    Xeno,
}

[RegisterComponent]
public sealed partial class HunterTranslatingMessageComponent : Component {}

[Serializable, NetSerializable]
public enum HunterTranslatorUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class HunterTranslatorBoundUserInterfaceState : BoundUserInterfaceState
{
    public HunterTranslationCategory TranslationTarget { get; }

    public HunterTranslatorBoundUserInterfaceState(HunterTranslationCategory translationTarget)
    {
        TranslationTarget = translationTarget;
    }
}

[Serializable, NetSerializable]
public sealed class HunterTranslatorSendMessage : BoundUserInterfaceMessage
{
    public string? Message { get; }
    public HunterTranslationCategory? TranslationTarget { get; }

    public HunterTranslatorSendMessage(string? message = null, HunterTranslationCategory? translationTarget = null)
    {
        Message = message;
        TranslationTarget = translationTarget;
    }
}


public sealed partial class HunterTranslatorActionEvent : InstantActionEvent {}
