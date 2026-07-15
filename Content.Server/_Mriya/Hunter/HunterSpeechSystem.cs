using Content.Server._RMC14.Chat.Chat;
using Content.Shared._Sich.Language;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Inventory;
using Robust.Shared.Player;

namespace Content.Server._Sich.Language;

/// <summary>
///     Система, що приховує мову мисливців від інших видів.
/// </summary>
public sealed class HunterSpeechSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    private readonly List<ICommonSession> _toRemove = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterLanguageComponent, ChatMessageAfterGetRecipients>(OnHunterAfterGetRecipients);
    }

    private void OnHunterAfterGetRecipients(EntityUid uid, HunterLanguageComponent component, ref ChatMessageAfterGetRecipients args)
    {
        var isTranslated = HasComp<HunterTranslatingMessageComponent>(uid);
        HunterTranslationCategory target = HunterTranslationCategory.All;

        if (isTranslated)
        {
            // Знаходимо перекладач, щоб дізнатися ціль
            if (TryComp<InventoryComponent>(uid, out var inv) &&
                _inventory.TryGetSlotEntity(uid, "gloves", out var translatorUid) &&
                TryComp<HunterTranslatorComponent>(translatorUid, out var translator))
            {
                target = translator.TranslationTarget;
            }
            // Якщо перекладача нема — за замовчуванням All (всі чують)
        }

        _toRemove.Clear();

        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer)
                continue;

            var recipient = session.AttachedEntity;
            if (recipient == null)
                continue;

            var isHunter = HasComp<HunterLanguageComponent>(recipient);
            if (isHunter)
                continue;

            if (isTranslated)
            {
                var isXeno = HasComp<XenoComponent>(recipient);
                var isHuman = !isXeno; // Спрощення

                if (target == HunterTranslationCategory.All)
                    continue;

                if (target == HunterTranslationCategory.Human && isHuman)
                    continue;

                if (target == HunterTranslationCategory.Xeno && isXeno)
                    continue;
            }

            // Ховаємо повідомлення від тих, хто не повинен його чути
            _toRemove.Add(session);
        }

        foreach (var session in _toRemove)
        {
            args.Recipients.Remove(session);
        }
    }
}
