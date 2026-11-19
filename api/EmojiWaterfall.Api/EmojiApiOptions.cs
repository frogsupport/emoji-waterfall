namespace EmojiWaterfall.Api;

using System.ComponentModel.DataAnnotations;

public sealed class EmojiApiOptions
{
    public const string SectionName = "EmojiApi";

    [Required]
    public required string AccessKey { get; set; }
}
