using System.Text.Json;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Models;

public sealed class UsageDtoTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateOnly Today = new(2026, 10, 7);
    private static readonly AppUsageDto Notepad = new(3, "Editor", 605);

    [Fact]
    public void AppUsageDto_ConstructionAndEquality()
    {
        Assert.Equal((3L, "Editor", 605L), (Notepad.AppId, Notepad.DisplayName, Notepad.Seconds));
        Assert.Equal(new AppUsageDto(3, "Editor", 605), Notepad);
        Assert.NotEqual(Notepad with { Seconds = 606 }, Notepad);
    }

    [Fact]
    public void DayUsageDto_ConstructionAndEquality()
    {
        AppUsageDto[] apps = [Notepad];
        var dto = new DayUsageDto(4, null, "S-1-5-21-1-2-3-1001", Today, Today, apps);

        Assert.Equal((4L, (Guid?)null, "S-1-5-21-1-2-3-1001", Today, Today), (dto.Revision, dto.LastChangeRequestId, dto.AccountSid, dto.Day, dto.ServiceToday));
        Assert.Equal(new DayUsageDto(4, null, "S-1-5-21-1-2-3-1001", Today, Today, apps), dto);
        Assert.NotEqual(dto with { Day = Today.AddDays(-1) }, dto);
    }

    [Fact]
    public void DayUsageDto_JsonRoundTrip_DateOnlyAndApps()
    {
        var dto = new DayUsageDto(4, null, "S-1-5-21-1-2-3-1001", Today.AddDays(-1), Today, [Notepad]);

        var json = JsonSerializer.Serialize(dto, WebOptions);
        var copy = JsonSerializer.Deserialize<DayUsageDto>(json, WebOptions);

        Assert.Contains("\"day\":\"2026-10-06\"", json, StringComparison.Ordinal);
        Assert.NotNull(copy);
        Assert.Equal((dto.Revision, dto.LastChangeRequestId, dto.AccountSid, dto.Day, dto.ServiceToday), (copy.Revision, copy.LastChangeRequestId, copy.AccountSid, copy.Day, copy.ServiceToday));
        Assert.Equal(dto.Apps, copy.Apps);
    }

    [Fact]
    public void AccountUsageDto_JsonRoundTrip_EmptyToday()
    {
        var today = new DayUsageDto(1, null, "S-1-5-21-1-2-3-1001", Today, Today, []);
        var dto = new AccountUsageDto("S-1-5-21-1-2-3-1001", Today, [today]);

        var copy = JsonSerializer.Deserialize<AccountUsageDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.NotNull(copy);
        Assert.Equal((dto.AccountSid, dto.ServiceToday, 1, 0), (copy.AccountSid, copy.ServiceToday, copy.Days.Count, copy.Days[0].Apps.Count));
    }

    [Fact]
    public void AccountUsageDto_Equality()
    {
        DayUsageDto[] days = [];

        Assert.Equal(new AccountUsageDto("S", Today, days), new AccountUsageDto("S", Today, days));
        Assert.NotEqual(new AccountUsageDto("S", Today, days), new AccountUsageDto("T", Today, days));
    }
}
