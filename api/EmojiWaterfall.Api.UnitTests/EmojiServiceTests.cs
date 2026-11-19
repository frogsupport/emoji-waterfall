using EmojiWaterfall.Api;

namespace EmojiWaterfall.Api.UnitTests;

public class EmojiServiceTests
{
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(200, 200)]
    [InlineData(500, 500)]
    [InlineData(501, 500)]
    [InlineData(5000, 500)]
    [InlineData(int.MaxValue, 500)]
    public void ClampCount_ReturnsEffectiveCount(int requested, int expected)
    {
        Assert.Equal(expected, EmojiService.ClampCount(requested));
    }
}
