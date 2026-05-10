using System.Text.RegularExpressions;
using Content.Server.Speech.Components;

namespace Content.Server.Speech.EntitySystems;

public sealed class FrontalLispSystem : EntitySystem
{
    // @formatter:off
    private static readonly Regex RegexUpperTh = new(@"[T]+[Ss]+|[S]+[Cc]+(?=[IiEeYy]+)|[C]+(?=[IiEeYy]+)|[P][Ss]+|([S]+[Tt]+|[T]+)(?=[Ii]+[Oo]+[Uu]*[Nn]*)|[C]+[Hh]+(?=[Ii]*[Ee]*)|[Z]+|[S]+|[X]+(?=[Ee]+)");
    private static readonly Regex RegexLowerTh = new(@"[t]+[s]+|[s]+[c]+(?=[iey]+)|[c]+(?=[iey]+)|[p][s]+|([s]+[t]+|[t]+)(?=[i]+[o]+[u]*[n]*)|[c]+[h]+(?=[i]*[e]*)|[z]+|[s]+|[x]+(?=[e]+)");
    private static readonly Regex RegexUpperEcks = new(@"[E]+[Xx]+[Cc]*|[X]+");
    private static readonly Regex RegexLowerEcks = new(@"[e]+[x]+[c]*|[x]+");
    // Sich start. Локалізація. Зроблено Pgriha за ідеєю France
    private static readonly Regex RegexUpperCyrillicZ = new(@"[Ж]");
    private static readonly Regex RegexLowerCyrillicZ = new(@"[ж]");
    private static readonly Regex RegexUpperCyrillicR = new(@"[Р]");
    private static readonly Regex RegexLowerCyrillicR = new(@"[р]");
    private static readonly Regex RegexUpperCyrillicS = new(@"[Ч]|[Ш]|[Щ]");
    private static readonly Regex RegexLowerCyrillicS = new(@"[ч]|[ш]|[щ]");
    // Sich end
    // @formatter:on

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrontalLispComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, FrontalLispComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // handles ts, sc(i|e|y), c(i|e|y), ps, st(io(u|n)), ch(i|e), z, s
        message = RegexUpperTh.Replace(message, "TH");
        message = RegexLowerTh.Replace(message, "th");
        // handles ex(c), x
        message = RegexUpperEcks.Replace(message, "EKTH");
        message = RegexLowerEcks.Replace(message, "ekth");
        // Sich start. Локалізація
        // Зузати (Жужати)
        message = RegexUpperCyrillicZ.Replace(message, "З");
        message = RegexLowerCyrillicZ.Replace(message, "з");
        // Лозмілковувати (Розмірковувати)
        message = RegexUpperCyrillicR.Replace(message, "Л");
        message = RegexLowerCyrillicR.Replace(message, "л");
        // Сарівна сляпа сповісала (Чарівна шляпа сповіщала)
        message = RegexUpperCyrillicS.Replace(message, "С");
        message = RegexLowerCyrillicS.Replace(message, "с");
        // Sich end

        args.Message = message;
    }
}
