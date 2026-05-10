using System.Text.RegularExpressions;
using Content.Server.Speech.Components;

namespace Content.Server.Speech.EntitySystems;

public sealed class MothAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerBuzz = new Regex("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new Regex("Z{1,3}");
    // Sich start. Локалізація
    private static readonly Regex RegexLowerCyrillicBzhh = new Regex("ж{1,3}");
    private static readonly Regex RegexUpperCyrillicBzhh = new Regex("Ж{1,3}");
    private static readonly Regex RegexLowerCyrillicBzz = new Regex("з{1,3}");
    private static readonly Regex RegexUpperCyrillicBzz = new Regex("З{1,3}");
    // Sich end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MothAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");
        // Sich start. Локалізація
        // бжжж
        message = RegexLowerCyrillicBzhh.Replace(message, "жжж");
        // БЖЖЖ
        message = RegexUpperCyrillicBzhh.Replace(message, "ЖЖЖ");
        // бззз
        message = RegexLowerCyrillicBzz.Replace(message, "ззз");
        // БЗЗЗ
        message = RegexUpperCyrillicBzz.Replace(message, "ЗЗЗ");
        // Sich end

        args.Message = message;
    }
}
