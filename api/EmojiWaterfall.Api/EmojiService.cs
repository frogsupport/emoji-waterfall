namespace EmojiWaterfall.Api;

using Microsoft.AspNetCore.WebUtilities;

public record Emoji
{
    public required string Slug { get; set; }
    public required string Character { get; set; }
    public required string UnicodeName { get; set; }
    public required string CodePoint { get; set; }
    public required string Group { get; set; }
    public required string SubGroup { get; set; }
}

public class EmojiService
{
    private readonly HttpClient _httpClient;

    private readonly string _emojiApiAccessKey;

    public EmojiService(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;

        _httpClient.BaseAddress = new Uri("https://emoji-api.com/");

        var emojiApiAccessKey = configuration["EmojiApi:AccessKey"];

        if (emojiApiAccessKey.IsWhiteSpace() || emojiApiAccessKey == null)
        {
            throw new Exception("No key boi");
        }

        _emojiApiAccessKey = emojiApiAccessKey;
    }

    public async Task<IReadOnlyList<Emoji>?> GetEmojisAsync(CancellationToken cancellationToken)
    {
        var uri = QueryHelpers.AddQueryString("emojis", "access_key", _emojiApiAccessKey);

        var emojis = await _httpClient.GetFromJsonAsync<List<Emoji>>(uri, cancellationToken);

        return emojis;
    }
}
