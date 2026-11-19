namespace EmojiWaterfall.Api;

public static class EmojiFilter
{
    // Windows has no glyphs for country flags (shown as two letters) or
    // subdivision flags (shown as a plain black flag), so we leave them out.
    public static bool IsUnsupportedFlag(string character)
    {
        foreach (var rune in character.EnumerateRunes())
        {
            // Regional Indicator Symbols (country flags) or tag characters (subdivision flags).
            if (rune.Value is >= 0x1F1E6 and <= 0x1F1FF or >= 0xE0020 and <= 0xE007F)
            {
                return true;
            }
        }

        return false;
    }
}
