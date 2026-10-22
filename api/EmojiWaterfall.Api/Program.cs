using EmojiWaterfall.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHybridCache();
builder.Services.AddHttpClient<EmojiService>();

var app = builder.Build();

app.MapGet("/", () => "This is emoji waterfall api boi");

app.MapGet("/api/health", () => new { status = "ok" });

// TODO: Add an IHostedService that fetches the emojis on startup

// /api/emojis?count={count}
app.MapGet(
    "/api/emojis",
    async (EmojiService emojiService, int count, CancellationToken cancellationToken) =>
        await emojiService.GetEmojisAsync(count, cancellationToken)
);

app.Run();
