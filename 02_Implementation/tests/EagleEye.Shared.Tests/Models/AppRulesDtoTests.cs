using System.Text.Json;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Models;

public sealed class AppRulesDtoTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
    private const string Sid = "S-1-5-21-1-2-3-1001";

    [Fact]
    public void AppRulesDto_JsonRoundTrip_AppsAndEmojiText()
    {
        var dto = new AppRulesDto(
            4, Guid.NewGuid(), Sid,
            [new AppRuleDto(7, "Editor", @"C:\Windows\System32\notepad.exe", 45, BreakTimeDays.Monday | BreakTimeDays.Friday)],
            "Schluss \U0001F60A\nZeile 2");

        var copy = JsonSerializer.Deserialize<AppRulesDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.NotNull(copy);
        Assert.Equal((dto.Revision, dto.LastChangeRequestId, dto.AccountSid, dto.BudgetText), (copy.Revision, copy.LastChangeRequestId, copy.AccountSid, copy.BudgetText));
        Assert.Equal(dto.Apps, copy.Apps);
    }

    [Fact]
    public void AppRulesDto_NullText_RoundTripsAsNull()
    {
        var dto = new AppRulesDto(1, null, Sid, [], null);

        var copy = JsonSerializer.Deserialize<AppRulesDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.Null(copy!.BudgetText);
    }

    [Fact]
    public void AppRuleDto_Equality()
    {
        var rule = new AppRuleDto(1, "A", "p", 0, BreakTimeDays.None);

        Assert.Equal(new AppRuleDto(1, "A", "p", 0, BreakTimeDays.None), rule);
        Assert.NotEqual(rule with { BudgetMinutes = 1 }, rule);
    }

    [Fact]
    public void WarningSettingsDto_JsonRoundTrip_WithAndWithoutTexts()
    {
        var dto = new WarningSettingsDto(3, Guid.NewGuid(), 5, null, 1, "Schnell! \U0001F680");

        var copy = JsonSerializer.Deserialize<WarningSettingsDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.Equal(dto, copy);
    }

    [Fact]
    public void KidMessageDto_JsonRoundTrip_AllFields()
    {
        var dto = new KidMessageDto(KidMessageReason.BreakTime, null, "Editor", 2, 1200, 1439);

        var copy = JsonSerializer.Deserialize<KidMessageDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.Equal(dto, copy);
    }

    [Fact]
    public void KidMessageReason_JsonRoundTrip_EveryValue()
    {
        foreach (var reason in Enum.GetValues<KidMessageReason>())
        {
            var dto = new KidMessageDto(reason, "t", null, null, null, null);

            Assert.Equal(dto, JsonSerializer.Deserialize<KidMessageDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions));
        }
    }

    [Fact]
    public void Enums_HaveThePlannedValues()
    {
        Assert.Equal([AppBudgetPart.Hours, AppBudgetPart.Minutes], Enum.GetValues<AppBudgetPart>());
        Assert.Equal([WarningLevel.Early, WarningLevel.Final], Enum.GetValues<WarningLevel>());
        Assert.Equal(
            [DefaultTextKind.BreakDisplayText, DefaultTextKind.BudgetText, DefaultTextKind.EarlyWarningText, DefaultTextKind.FinalWarningText],
            Enum.GetValues<DefaultTextKind>());
        Assert.Equal(9, Enum.GetValues<KidMessageReason>().Length);
    }
}
