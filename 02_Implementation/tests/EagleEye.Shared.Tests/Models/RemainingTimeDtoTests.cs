using System.Text.Json;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Models;

public sealed class RemainingTimeDtoTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void RemainingTimeDto_JsonRoundTrip_LinesMinutesSignal()
    {
        var dto = new RemainingTimeDto(
            [
                new OverlayLineDto(OverlayLineKind.Group, "Games 🎮", 252, OverlayLevel.Early, true),
                new OverlayLineDto(OverlayLineKind.Break, null, 1110, OverlayLevel.Normal, true),
                new OverlayLineDto(OverlayLineKind.App, "Editor", 0, OverlayLevel.Expired, false),
            ],
            5, 1, OverlaySignal.Final);

        var copy = JsonSerializer.Deserialize<RemainingTimeDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.NotNull(copy);
        Assert.Equal((dto.EarlyMinutes, dto.FinalMinutes, dto.Signal), (copy.EarlyMinutes, copy.FinalMinutes, copy.Signal));
        Assert.Equal(dto.Lines, copy.Lines);
    }

    [Fact]
    public void RemainingTimeDto_EmptyLines_RoundTrip()
    {
        var dto = new RemainingTimeDto([], 5, 1, OverlaySignal.None);

        var copy = JsonSerializer.Deserialize<RemainingTimeDto>(JsonSerializer.Serialize(dto, WebOptions), WebOptions);

        Assert.NotNull(copy);
        Assert.Empty(copy.Lines);
    }

    [Fact]
    public void OverlayLineDto_Equality()
    {
        Assert.Equal(new OverlayLineDto(OverlayLineKind.App, "a", 1, OverlayLevel.Final, true), new OverlayLineDto(OverlayLineKind.App, "a", 1, OverlayLevel.Final, true));
        Assert.NotEqual(new OverlayLineDto(OverlayLineKind.App, "a", 1, OverlayLevel.Final, true), new OverlayLineDto(OverlayLineKind.App, "a", 1, OverlayLevel.Final, false));
    }
}
