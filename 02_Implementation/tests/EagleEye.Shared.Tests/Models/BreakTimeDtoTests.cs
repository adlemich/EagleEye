using System.Text.Json;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Models;

public sealed class BreakTimeDtoTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
    private const string Sid = "S-1-5-21-1-2-3-1001";

    [Fact]
    public void BreakTimeEntryDto_ConstructionAndEquality()
    {
        var entry = new BreakTimeEntryDto(3, true, 1200, 1439, BreakTimeDays.Monday | BreakTimeDays.Friday);

        Assert.Equal((3L, true, 1200, 1439, (BreakTimeDays)17), (entry.EntryId, entry.IsActive, entry.StartMinute, entry.EndMinute, entry.Days));
        Assert.Equal(new BreakTimeEntryDto(3, true, 1200, 1439, (BreakTimeDays)17), entry);
        Assert.NotEqual(entry with { IsActive = false }, entry);
    }

    [Fact]
    public void AccountRulesDto_JsonRoundTrip_EntriesEnumsAndEmojiText()
    {
        var requestId = Guid.NewGuid();
        var dto = new AccountRulesDto(
            7, requestId, Sid,
            [new BreakTimeEntryDto(1, false, 0, 540, BreakTimeDays.Saturday | BreakTimeDays.Sunday)],
            "Pause \U0001F60A \U0001F4DA \U0001F44D\n\U0001F468‍\U0001F469‍\U0001F467 \U0001F44D\U0001F3FD", false);

        var json = JsonSerializer.Serialize(dto, WebOptions);
        var copy = JsonSerializer.Deserialize<AccountRulesDto>(json, WebOptions);

        Assert.NotNull(copy);
        Assert.Equal((dto.Revision, dto.LastChangeRequestId, dto.AccountSid, dto.DisplayText, dto.IsDefaultDisplayText), (copy.Revision, copy.LastChangeRequestId, copy.AccountSid, copy.DisplayText, copy.IsDefaultDisplayText));
        Assert.Equal(dto.BreakTimes, copy.BreakTimes);
    }

    [Fact]
    public void AccountRulesDto_Equality()
    {
        BreakTimeEntryDto[] entries = [];
        var dto = new AccountRulesDto(1, null, Sid, entries, "x", true);

        Assert.Equal(new AccountRulesDto(1, null, Sid, entries, "x", true), dto);
        Assert.NotEqual(dto with { DisplayText = "y" }, dto);
    }

    [Fact]
    public void BreakTimeMessageDto_JsonRoundTrip()
    {
        var dto = new BreakTimeMessageDto("Hallo \U0001F60A\nZeile 2");

        var copy = JsonSerializer.Deserialize<BreakTimeMessageDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.Equal(dto, copy);
    }

    [Theory]
    [InlineData(KidMessageResult.Shown)]
    [InlineData(KidMessageResult.AlreadyOpen)]
    public void KidMessageResult_JsonRoundTrip(KidMessageResult result)
    {
        var copy = JsonSerializer.Deserialize<KidMessageResult>(JsonSerializer.Serialize(result, WebOptions), WebOptions);

        Assert.Equal(result, copy);
    }

    [Fact]
    public void BreakTimeDays_AllIsTheSevenDays()
    {
        var all = BreakTimeDays.Monday | BreakTimeDays.Tuesday | BreakTimeDays.Wednesday | BreakTimeDays.Thursday
            | BreakTimeDays.Friday | BreakTimeDays.Saturday | BreakTimeDays.Sunday;

        Assert.Equal(BreakTimeDays.All, all);
    }

    [Fact]
    public void BreakTimeBoundary_HasStartAndEnd()
    {
        Assert.Equal([BreakTimeBoundary.Start, BreakTimeBoundary.End], Enum.GetValues<BreakTimeBoundary>());
    }
}
