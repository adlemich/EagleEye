using System.Text.Json;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Models;

public sealed class UserAccountDtoTests
{
    // SignalR's JSON hub protocol uses System.Text.Json with the web defaults (camelCase, case-insensitive).
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static readonly UserAccountDto Max = new("S-1-5-21-1-2-3-1001", "max", "Max Adler", false, true);
    private static readonly UserAccountDto Anna = new("S-1-5-21-1-2-3-1002", "anna", null, true, false);

    [Fact]
    public void UserAccountDto_Constructor_SetsProperties()
    {
        Assert.Equal(
            ("S-1-5-21-1-2-3-1001", "max", "Max Adler", false, true),
            (Max.Sid, Max.UserName, Max.FullName, Max.IsDisabled, Max.IsUnderParentalControl));
    }

    [Fact]
    public void UserAccountDto_Equals_SameValues_ReturnsTrue()
    {
        Assert.Equal(new UserAccountDto("S-1-5-21-1-2-3-1001", "max", "Max Adler", false, true), Max);
    }

    [Fact]
    public void UserAccountDto_Equals_DifferentSelection_ReturnsFalse()
    {
        Assert.NotEqual(Max with { IsUnderParentalControl = false }, Max);
    }

    [Fact]
    public void UserAccountDto_JsonRoundTrip_KeepsAllFieldsIncludingNullFullName()
    {
        var json = JsonSerializer.Serialize(Anna, WebOptions);

        Assert.Equal(Anna, JsonSerializer.Deserialize<UserAccountDto>(json, WebOptions));
    }

    [Fact]
    public void UserAccountListDto_Constructor_SetsProperties()
    {
        var requestId = Guid.NewGuid();
        UserAccountDto[] accounts = [Anna, Max];

        var dto = new UserAccountListDto(7, requestId, accounts);

        Assert.Equal((7L, (Guid?)requestId, (IReadOnlyList<UserAccountDto>)accounts), (dto.Revision, dto.LastChangeRequestId, dto.Accounts));
    }

    [Fact]
    public void UserAccountListDto_Equals_SameListInstance_ReturnsTrue()
    {
        UserAccountDto[] accounts = [Max];

        Assert.Equal(new UserAccountListDto(1, null, accounts), new UserAccountListDto(1, null, accounts));
    }

    [Fact]
    public void UserAccountListDto_Equals_DifferentRevision_ReturnsFalse()
    {
        UserAccountDto[] accounts = [Max];

        Assert.NotEqual(new UserAccountListDto(1, null, accounts), new UserAccountListDto(2, null, accounts));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UserAccountListDto_JsonRoundTrip_KeepsRevisionRequestIdAndAccounts(bool withRequestId)
    {
        Guid? requestId = withRequestId ? Guid.Parse("7f3c0000-0000-0000-0000-000000000001") : null;
        var dto = new UserAccountListDto(42, requestId, [Anna, Max]);

        var copy = JsonSerializer.Deserialize<UserAccountListDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.NotNull(copy);
        Assert.Equal((dto.Revision, dto.LastChangeRequestId), (copy.Revision, copy.LastChangeRequestId));
        Assert.Equal(dto.Accounts, copy.Accounts);
    }

    [Fact]
    public void UserAccountListDto_Json_UsesCamelCaseNames()
    {
        var json = JsonSerializer.Serialize(new UserAccountListDto(1, null, [Max]), WebOptions);

        Assert.Contains("\"lastChangeRequestId\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void StateWriteAckDto_ConstructorAndEquality()
    {
        Assert.Equal((13L, new StateWriteAckDto(13)), (new StateWriteAckDto(13).Revision, new StateWriteAckDto(13)));
    }

    [Fact]
    public void StateWriteAckDto_Equals_DifferentRevision_ReturnsFalse()
    {
        Assert.NotEqual(new StateWriteAckDto(13), new StateWriteAckDto(14));
    }

    [Fact]
    public void StateWriteAckDto_JsonRoundTrip_KeepsRevision()
    {
        var json = JsonSerializer.Serialize(new StateWriteAckDto(long.MaxValue), WebOptions);

        Assert.Equal(new StateWriteAckDto(long.MaxValue), JsonSerializer.Deserialize<StateWriteAckDto>(json, WebOptions));
    }
}
