using System.Text.RegularExpressions;
using Content.Server.Speech.Components;

namespace Content.Server.Speech.EntitySystems;

public sealed class LizardAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerS = new("s+");
    private static readonly Regex RegexUpperS = new("S+");
    private static readonly Regex RegexInternalX = new(@"(\w)x");
    private static readonly Regex RegexLowerEndX = new(@"\bx([\-|r|R]|\b)");
    private static readonly Regex RegexUpperEndX = new(@"\bX([\-|r|R]|\b)");
    // Sich start. Локалізація
    private static readonly Regex RegexLowerCyrillicS = new("с+");
    private static readonly Regex RegexUpperCyrillicS = new("С+");
    private static readonly Regex RegexLowerCyrillicSh = new("ш+");
    private static readonly Regex RegexUpperCyrillicSh = new("Ш+");
    private static readonly Regex RegexLowerCyrillicShch = new("щ+");
    private static readonly Regex RegexUpperCyrillicShch = new("Щ+");
    private static readonly Regex RegexLowerCyrillicZh = new("ж+");
    private static readonly Regex RegexUpperCyrillicZh = new("Ж+");
    // Sich end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LizardAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, LizardAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // hissss
        message = RegexLowerS.Replace(message, "sss");
        // hiSSS
        message = RegexUpperS.Replace(message, "SSS");
        // ekssit
        message = RegexInternalX.Replace(message, "$1kss");
        // ecks
        message = RegexLowerEndX.Replace(message, "ecks$1");
        // eckS
        message = RegexUpperEndX.Replace(message, "ECKS$1");
        // Sich start. Локалізація
        // сссупер
        message = RegexLowerCyrillicS.Replace(message, "ссс");
        // СССУПЕР
        message = RegexUpperCyrillicS.Replace(message, "ССС");
        // шшшипіти
        message = RegexLowerCyrillicSh.Replace(message, "шшш");
        // ШШШИПІТИ
        message = RegexUpperCyrillicSh.Replace(message, "ШШШ");
        // шшщука
        message = RegexLowerCyrillicShch.Replace(message, "шшщ");
        // ШШЩУКА
        message = RegexUpperCyrillicShch.Replace(message, "ШШЩ");
        // жшшнець
        message = RegexLowerCyrillicZh.Replace(message, "жшш");
        // ЖШШнець
        message = RegexUpperCyrillicZh.Replace(message, "ЖШШ");
        // Sich end

        args.Message = message;
    }
}
