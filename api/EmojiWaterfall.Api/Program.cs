using EmojiWaterfall.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHybridCache();
builder.Services.AddHttpClient<EmojiService>();
builder.Services.AddHostedService<StartupFetchService>();

var app = builder.Build();

app.MapGet("/", () => "This is emoji waterfall api boi");

app.MapGet("/api/health", () => new { status = "ok" });

// /api/emojis?count={count}
app.MapGet(
    "/api/emojis",
    async (EmojiService emojiService, int count, CancellationToken cancellationToken) =>
        await emojiService.GetEmojisAsync(count, cancellationToken)
);

app.Run();
