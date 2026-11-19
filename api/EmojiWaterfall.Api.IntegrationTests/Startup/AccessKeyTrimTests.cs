using EmojiWaterfall.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EmojiWaterfall.Api.IntegrationTests.Startup;

public sealed class AccessKeyTrimTests
{
    // A Docker secret file often ends with a newline, and the key-per-file
    // provider keeps it. emoji-api.com would reject a key with a newline.
    [Theory]
    [InlineData("abc123\n")]
    [InlineData("abc123\r\n")]
    [InlineData("  abc123  ")]
    public void Should_Trim_Whitespace_From_Access_Key(string accessKey)
    {
        using var factory = new EmojiWaterfallWebApplicationFactory().WithWebHostBuilder(b =>
            b.UseSetting("EmojiApi:AccessKey", accessKey)
        );

        var options = factory.Services.GetRequiredService<IOptions<EmojiApiOptions>>();

        Assert.Equal("abc123", options.Value.AccessKey);
    }
}
