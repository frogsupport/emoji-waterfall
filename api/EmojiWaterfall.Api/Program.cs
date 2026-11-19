using EmojiWaterfall.Api;

var builder = WebApplication.CreateBuilder(args);

// In Docker Swarm, each secret is a file in /run/secrets. The file name is the
// setting name, so a file named EmojiApi__AccessKey sets EmojiApi:AccessKey.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

// Check the key when the app starts, not on the first request.
// Trim first: a secret file often ends with a newline, and the provider keeps it.
builder
    .Services.AddOptions<EmojiApiOptions>()
    .BindConfiguration(EmojiApiOptions.SectionName)
    .PostConfigure(options => options.AccessKey = options.AccessKey?.Trim()!)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHybridCache();
builder.Services.AddHttpClient<EmojiService>();
builder.Services.AddHostedService<StartupFetchService>();

var app = builder.Build();

app.MapGet("/api/health", () => new { status = "ok" });

// /api/emojis?count=<count>
// count is limited to EmojiService.MaxCount (500); a larger count returns 500 emojis.
app.MapGet(
    "/api/emojis",
    async (EmojiService emojiService, int count, CancellationToken cancellationToken) =>
        await emojiService.GetEmojisAsync(count, cancellationToken)
);

app.Run();
