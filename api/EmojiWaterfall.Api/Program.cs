using EmojiWaterfall.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<EmojiService>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/health", () => new { status = "ok" });

app.MapGet(
    "/api/emojis",
    async (EmojiService emojiService, CancellationToken cancellationToken) =>
        await emojiService.GetEmojisAsync(cancellationToken)
);

app.Run();
