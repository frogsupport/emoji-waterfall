namespace EmojiWaterfall.Api;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Hybrid;

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

    private readonly HybridCache _hybridCache;

    private const string emojiCacheKey = "emoji-api:all-emojis";

    public EmojiService(
        IConfiguration configuration,
        HttpClient httpClient,
        HybridCache hybridCache
    )
    {
        _httpClient = httpClient;

        _httpClient.BaseAddress = new Uri("https://emoji-api.com/");

        var emojiApiAccessKey = configuration["EmojiApi:AccessKey"];

        if (emojiApiAccessKey == null || emojiApiAccessKey.IsWhiteSpace())
        {
            throw new Exception("No key boi");
        }

        _emojiApiAccessKey = emojiApiAccessKey;

        _hybridCache = hybridCache;
    }

    private async Task<List<Emoji>?> GetAllEmojisAsync(CancellationToken cancellationToken)
    {
        var cachedEmojis = await _hybridCache.GetOrCreateAsync(
            emojiCacheKey,
            async factoryCancellationToken =>
            {
                var uri = QueryHelpers.AddQueryString("emojis", "access_key", _emojiApiAccessKey);

                var emojis = await _httpClient.GetFromJsonAsync<List<Emoji>>(
                    uri,
                    factoryCancellationToken
                );

                return emojis;
            },
            new HybridCacheEntryOptions()
            {
                Expiration = TimeSpan.FromHours(24),
                LocalCacheExpiration = TimeSpan.FromHours(24),
            },
            cancellationToken: cancellationToken
        );

        return cachedEmojis;
    }

    public async Task PrimeCacheAsync(CancellationToken cancellationToken)
    {
        await GetAllEmojisAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Emoji>?> GetEmojisAsync(
        int count,
        CancellationToken cancellationToken
    )
    {
        var emojis = await GetAllEmojisAsync(cancellationToken);

        return emojis?.Shuffle().Take(count).ToList();
    }
}
