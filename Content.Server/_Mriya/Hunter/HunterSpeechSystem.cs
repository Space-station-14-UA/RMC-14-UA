using Content.Shared._Sich.Language;
using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Sich.Language;

/// <summary>
///     Система, що дає мисливцям можливість розуміти всі мови.
/// </summary>
public sealed class HunterSpeechSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterLanguageComponent, DetermineEntityLanguagesEvent>(OnDetermineEntityLanguages);
        SubscribeLocalEvent<HunterTranslatingMessageComponent, DetermineLanguageEvent>(OnDetermineTranslatingLanguage);
    }

    private void OnDetermineTranslatingLanguage(EntityUid uid, HunterTranslatingMessageComponent component, ref DetermineLanguageEvent args)
    {
        // Mriya. Вибираємо мову в залежності від режиму перекладача.
        args.Language = component.TranslationTarget == HunterTranslationCategory.Xeno
            ? new ProtoId<LanguagePrototype>("Xeno")
            : SharedLanguageSystem.CommonLanguage;
    }

    private void OnDetermineEntityLanguages(Entity<HunterLanguageComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        if (!ent.Comp.UnderstandsAllLanguages)
            return;

        foreach (var langProto in _prototypeManager.EnumeratePrototypes<LanguagePrototype>())
        {
            args.UnderstoodLanguages.Add(langProto.ID);
        }
    }
}
