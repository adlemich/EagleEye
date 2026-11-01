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
        { Dto(KidMessageReason.AppNotAllowedToday, "Editor"), "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.", "Editor: heute nicht erlaubt." },
        { Dto(KidMessageReason.AppBudgetUsedUp, "Editor"), "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.", "Editor: Zeit-Budget aufgebraucht." },
        { Dto(KidMessageReason.BreakTime, null, 1200, 1439), EagleEye.Shared.Constants.BreakTimeRules.DefaultDisplayText, "Ruhezeit von 20:00 bis 24:00." },
        { Dto(KidMessageReason.BreakTime, null, 450, 1200), EagleEye.Shared.Constants.BreakTimeRules.DefaultDisplayText, "Ruhezeit von 07:30 bis 20:00." },
    };

    public static TheoryData<KidMessageDto, string, string> EnglishInfo => new()
    {
        { Dto(KidMessageReason.AppNotAllowedToday, "Notepad"), "You have used up your time! Sorry, that's it for now.", "Notepad: not allowed today." },
        { Dto(KidMessageReason.AppBudgetUsedUp, "Notepad"), "You have used up your time! Sorry, that's it for now.", "Notepad: time budget used up." },
        {
            Dto(KidMessageReason.BreakTime, null, 1200, 1439),
            "Hi! Unfortunately, your parents have set a PC break for this time. You can't use this program right now. Sorry. How about using the time to read a book? \U0001F60A",
            "Break time from " + T(1200) + " to midnight."
        },
    };

    /// <summary>US-008: the service puts the group name into AppName; the tray renders it unchanged (no tray code change).</summary>
    public static TheoryData<KidMessageReason, string, string> GroupInfo => new()
    {
        { KidMessageReason.AppNotAllowedToday, "Games: heute nicht erlaubt.", "Games: not allowed today." },
        { KidMessageReason.AppBudgetUsedUp, "Games: Zeit-Budget aufgebraucht.", "Games: time budget used up." },
    };

    [Theory]
    [MemberData(nameof(GroupInfo))]
    public void Compose_GroupName_InTheInfoLine_GermanAndEnglish(KidMessageReason reason, string german, string english)
    {
        var message = Dto(reason, "Games");

        Assert.Equal((german, english), (KidMessageText.Compose(message, German, German).Info, KidMessageText.Compose(message, English, English).Info));
    }

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
        Assert.Equal("Break time from 07:30 to 20:00.", KidMessageText.Compose(Dto(KidMessageReason.BreakTime, null, 450, 1200), English, British).Info);
    }

    [Fact]
    public void Compose_OtherLanguage_German()
    {
        Assert.Equal("Editor: heute nicht erlaubt.", KidMessageText.Compose(Dto(KidMessageReason.AppNotAllowedToday, "Editor"), CultureInfo.GetCultureInfo("fr-FR"), German).Info);
    }

    [Fact]
    public void Compose_MissingValues_Defaults()
    {
        Assert.Equal(": Zeit-Budget aufgebraucht.", KidMessageText.Compose(new KidMessageDto(KidMessageReason.AppBudgetUsedUp, "x", null, null, null), German, German).Info);
        Assert.Equal("Ruhezeit von  bis .", KidMessageText.Compose(new KidMessageDto(KidMessageReason.BreakTime, "x", null, null, null), German, German).Info);
    }

    [Fact]
    public void Compose_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => KidMessageText.Compose(null!, German, German));
        Assert.Throws<ArgumentNullException>(() => KidMessageText.Compose(Dto(KidMessageReason.BreakTime, null), null!, German));
        Assert.Throws<ArgumentNullException>(() => KidMessageText.Compose(Dto(KidMessageReason.BreakTime, null), German, null!));
    }

    [Theory]
    [InlineData(KidMessageReason.AppNotAllowedToday, DefaultTextKind.BudgetText)]
    [InlineData(KidMessageReason.AppBudgetUsedUp, DefaultTextKind.BudgetText)]
    [InlineData(KidMessageReason.BreakTime, DefaultTextKind.BreakDisplayText)]
    public void KindOf_EveryReason(KidMessageReason reason, DefaultTextKind expected)
    {
        Assert.Equal(expected, KidMessageText.KindOf(reason));
    }

    [Theory]
    [InlineData("InfoMinutesLeftOne")]
    [InlineData("InfoMinutesLeftMany")]
    [InlineData("InfoBreakBegins")]
    [InlineData("InfoAppGraceOne")]
    [InlineData("InfoAppGraceMany")]
    [InlineData("InfoBreakGraceOne")]
    [InlineData("InfoBreakGraceMany")]
    public void WarningInfoLines_Removed(string key)
    {
        Assert.Throws<InvalidOperationException>(() => TrayTexts.Get(key, German));
    }

    private static string T(int minute) => DateTime.MinValue.AddMinutes(minute).ToString("t", English);

    private static KidMessageDto Dto(KidMessageReason reason, string? app, int? start = null, int? end = null) =>
        new(reason, null, app, start, end);
}
