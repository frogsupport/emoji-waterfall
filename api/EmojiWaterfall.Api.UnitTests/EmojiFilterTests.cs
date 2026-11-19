using EmojiWaterfall.Api;

namespace EmojiWaterfall.Api.UnitTests;

public class EmojiFilterTests
{
    [Theory]
    [InlineData("\U0001F1FA\U0001F1F8")] // US flag
    [InlineData("\U0001F1F8\U0001F1FE")] // Syria flag
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F")] // England
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0073\U000E0063\U000E0074\U000E007F")] // Scotland
    public void IsUnsupportedFlag_ReturnsTrue_ForCountryAndSubdivisionFlags(string character)
    {
        Assert.True(EmojiFilter.IsUnsupportedFlag(character));
    }

    [Theory]
    [InlineData("\U0001F600")] // grinning face
    [InlineData("\U0001F349")] // watermelon
    [InlineData("\U0001F3C1")] // chequered flag
    [InlineData("\U0001F6A9")] // triangular flag
    [InlineData("\U0001F38C")] // crossed flags
    [InlineData("\U0001F3F4")] // black flag
    [InlineData("\U0001F3F3\uFE0F")] // white flag
    [InlineData("\U0001F3F3\uFE0F\u200D\U0001F308")] // rainbow flag
    [InlineData("\U0001F3F4\u200D\u2620\uFE0F")] // pirate flag
    [InlineData("1\uFE0F\u20E3")] // keycap 1
    [InlineData("")]
    public void IsUnsupportedFlag_ReturnsFalse_ForOtherEmojis(string character)
    {
        Assert.False(EmojiFilter.IsUnsupportedFlag(character));
    }
}
