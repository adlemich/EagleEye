using System.Globalization;
using EagleEye.Shared.Models;
using EagleEye.TrayClient.UI;
using Xunit;

namespace EagleEye.TrayClient.Tests.UI;

public sealed class KidMessageTextTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");

    public static TheoryData<KidMessageDto, string, string> GermanInfo => new()
    {
        { Dto(KidMessageReason.AppEarlyWarning, "Editor", 2), "Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt.", "Editor: noch 2 Minuten." },
        { Dto(KidMessageReason.AppFinalWarning, "Editor", 1), "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!", "Editor: noch 1 Minute." },
        { Dto(KidMessageReason.BreakEarlyWarning, null, 5, 1200), "Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt.", "Die Ruhezeit beginnt um 20:00." },
        { Dto(KidMessageReason.BreakFinalWarning, null, 1, 0), "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!", "Die Ruhezeit beginnt um 00:00." },
        { Dto(KidMessageReason.AppGracePeriod, "Editor", 1), "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!", "Editor: Deine Eltern haben die Regeln geändert. Editor wird in 1 Minute geschlossen." },
        { Dto(KidMessageReason.AppGracePeriod, "Editor", 3), "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!", "Editor: Deine Eltern haben die Regeln geändert. Editor wird in 3 Minuten geschlossen." },
        { Dto(KidMessageReason.BreakGracePeriod, null, 1, 1200), "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!", "Deine Eltern haben eine Ruhezeit eingestellt. Alle Apps werden in 1 Minute geschlossen." },
        { Dto(KidMessageReason.BreakGracePeriod, null, 2, 1200), "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!", "Deine Eltern haben eine Ruhezeit eingestellt. Alle Apps werden in 2 Minuten geschlossen." },
        { Dto(KidMessageReason.AppNotAllowedToday, "Editor"), "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.", "Editor: heute nicht erlaubt." },
        { Dto(KidMessageReason.AppBudgetUsedUp, "Editor"), "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.", "Editor: Zeit-Budget aufgebraucht." },
        { Dto(KidMessageReason.BreakTime, null, null, 1200, 1439), EagleEye.Shared.Constants.BreakTimeRules.DefaultDisplayText, "Ruhezeit von 20:00 bis 24:00." },
        { Dto(KidMessageReason.BreakTime, null, null, 450, 1200), EagleEye.Shared.Constants.BreakTimeRules.DefaultDisplayText, "Ruhezeit von 07:30 bis 20:00." },
    };

    public static TheoryData<KidMessageDto, string, string> EnglishInfo => new()
    {
        { Dto(KidMessageReason.AppEarlyWarning, "Notepad", 2), "Attention, you only have 5 minutes left! Save your progress.", "Notepad: 2 minutes left." },
        { Dto(KidMessageReason.AppFinalWarning, "Notepad", 1), "Attention!!! Only 1 minute left – save quickly now!", "Notepad: 1 minute left." },
        { Dto(KidMessageReason.BreakEarlyWarning, null, 5, 1200), "Attention, you only have 5 minutes left! Save your progress.", "Break time begins at " + T(1200) + "." },
        { Dto(KidMessageReason.AppGracePeriod, "Notepad", 1), "Attention!!! Only 1 minute left – save quickly now!", "Notepad: your parents changed the rules. Notepad closes in 1 minute." },
        { Dto(KidMessageReason.AppGracePeriod, "Notepad", 2), "Attention!!! Only 1 minute left – save quickly now!", "Notepad: your parents changed the rules. Notepad closes in 2 minutes." },
        { Dto(KidMessageReason.BreakGracePeriod, null, 1, 1200), "Attention!!! Only 1 minute left – save quickly now!", "Your parents set a break time. All apps close in 1 minute." },
        { Dto(KidMessageReason.BreakGracePeriod, null, 4, 1200), "Attention!!! Only 1 minute left – save quickly now!", "Your parents set a break time. All apps close in 4 minutes." },
        { Dto(KidMessageReason.AppNotAllowedToday, "Notepad"), "You have used up your time! Sorry, that's it for now.", "Notepad: not allowed today." },
        { Dto(KidMessageReason.AppBudgetUsedUp, "Notepad"), "You have used up your time! Sorry, that's it for now.", "Notepad: time budget used up." },
        {
            Dto(KidMessageReason.BreakTime, null, null, 1200, 1439),
            "Hi! Unfortunately, your parents have set a PC break for this time. You can't use this program right now. Sorry. How about using the time to read a book? \U0001F60A",
            "Break time from " + T(1200) + " to midnight."
        },
    };

    [Theory]
    [MemberData(nameof(GermanInfo))]
    public void Compose_German_DefaultTextAndInfoLine(KidMessageDto message, string text, string info)
    {
        Assert.Equal(new KidMessageContent(text, info), KidMessageText.Compose(message, German, German));
    }

    [Theory]
    [MemberData(nameof(EnglishInfo))]
    public void Compose_English_DefaultTextAndInfoLine(KidMessageDto message, string text, string info)
    {
        Assert.Equal(new KidMessageContent(text, info), KidMessageText.Compose(message, English, English));
    }

    [Fact]
    public void Compose_ParentText_ShownExactlyInEveryLanguage()
    {
        var message = Dto(KidMessageReason.AppBudgetUsedUp, "Editor") with { ParentText = "Schluss \U0001F60A\nfür heute" };

        Assert.Equal("Schluss \U0001F60A\nfür heute", KidMessageText.Compose(message, English, English).Text);
        Assert.Equal("Schluss \U0001F60A\nfür heute", KidMessageText.Compose(message, German, German).Text);
    }

    [Fact]
    public void Compose_TimesInTheKidsFormat()
    {
        Assert.Equal("Break time begins at 20:00.", KidMessageText.Compose(Dto(KidMessageReason.BreakEarlyWarning, null, 5, 1200), English, British).Info);
    }

    [Fact]
    public void Compose_OtherLanguage_German()
    {
        Assert.Equal("Editor: noch 2 Minuten.", KidMessageText.Compose(Dto(KidMessageReason.AppEarlyWarning, "Editor", 2), CultureInfo.GetCultureInfo("fr-FR"), German).Info);
    }

    [Fact]
    public void Compose_MissingValues_Defaults()
    {
        Assert.Equal(": noch 1 Minute.", KidMessageText.Compose(new KidMessageDto(KidMessageReason.AppFinalWarning, "x", null, null, null, null), German, German).Info);
    }

    [Fact]
    public void Compose_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => KidMessageText.Compose(null!, German, German));
        Assert.Throws<ArgumentNullException>(() => KidMessageText.Compose(Dto(KidMessageReason.BreakTime, null), null!, German));
        Assert.Throws<ArgumentNullException>(() => KidMessageText.Compose(Dto(KidMessageReason.BreakTime, null), German, null!));
    }

    [Theory]
    [InlineData(KidMessageReason.AppEarlyWarning, DefaultTextKind.EarlyWarningText)]
    [InlineData(KidMessageReason.BreakEarlyWarning, DefaultTextKind.EarlyWarningText)]
    [InlineData(KidMessageReason.AppFinalWarning, DefaultTextKind.FinalWarningText)]
    [InlineData(KidMessageReason.BreakFinalWarning, DefaultTextKind.FinalWarningText)]
    [InlineData(KidMessageReason.AppGracePeriod, DefaultTextKind.FinalWarningText)]
    [InlineData(KidMessageReason.BreakGracePeriod, DefaultTextKind.FinalWarningText)]
    [InlineData(KidMessageReason.AppNotAllowedToday, DefaultTextKind.BudgetText)]
    [InlineData(KidMessageReason.AppBudgetUsedUp, DefaultTextKind.BudgetText)]
    [InlineData(KidMessageReason.BreakTime, DefaultTextKind.BreakDisplayText)]
    public void KindOf_EveryReason(KidMessageReason reason, DefaultTextKind expected)
    {
        Assert.Equal(expected, KidMessageText.KindOf(reason));
    }

    private static string T(int minute) => DateTime.MinValue.AddMinutes(minute).ToString("t", English);

    private static KidMessageDto Dto(KidMessageReason reason, string? app, int? minutes = null, int? start = null, int? end = null) =>
        new(reason, null, app, minutes, start, end);
}
