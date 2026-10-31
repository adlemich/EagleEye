using System.Text.Json;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Models;

public sealed class AppGroupDtoTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid RequestId = Guid.Parse("4b0b6c1e-6f3e-4b7c-9a52-0f8f3b1d2c11");

    [Fact]
    public void AppGroupsDto_JsonRoundTrip_GroupsAndApps()
    {
        var dto = new AppGroupsDto(
            5, RequestId, [new AppGroupDto(1, "Games 🎮")],
            [new GroupableAppDto(@"C:\Games\launcher.exe", "Launcher", 1), new GroupableAppDto(@"C:\Windows\notepad.exe", "Editor", null)]);

        var copy = JsonSerializer.Deserialize<AppGroupsDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.NotNull(copy);
        Assert.Equal((dto.Revision, dto.LastChangeRequestId), (copy.Revision, copy.LastChangeRequestId));
        Assert.Equal(dto.Groups, copy.Groups);
        Assert.Equal(dto.Apps, copy.Apps);
    }

    [Fact]
    public void AppGroupRulesDto_JsonRoundTrip_Rules()
    {
        var dto = new AppGroupRulesDto(6, null, "S-1-5-21-1", [new AppGroupRuleDto(1, 60, BreakTimeDays.Saturday | BreakTimeDays.Sunday)]);

        var copy = JsonSerializer.Deserialize<AppGroupRulesDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.NotNull(copy);
        Assert.Equal((dto.Revision, dto.LastChangeRequestId, dto.AccountSid), (copy.Revision, copy.LastChangeRequestId, copy.AccountSid));
        Assert.Equal(dto.Rules, copy.Rules);
    }

    [Fact]
    public void Records_Equality()
    {
        Assert.Equal(new AppGroupDto(1, "Games"), new AppGroupDto(1, "Games"));
        Assert.NotEqual(new GroupableAppDto("p", "n", 1), new GroupableAppDto("p", "n", null));
        Assert.NotEqual(new AppGroupRuleDto(1, 60, BreakTimeDays.None), new AppGroupRuleDto(1, 61, BreakTimeDays.None));
        Assert.Equal((7L, "Games", 720L), (new GroupUsageDto(7, "Games", 720).GroupId, new GroupUsageDto(7, "Games", 720).Name, new GroupUsageDto(7, "Games", 720).Seconds));
    }
}
