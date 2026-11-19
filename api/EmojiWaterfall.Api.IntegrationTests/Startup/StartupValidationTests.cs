using EmojiWaterfall.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace EmojiWaterfall.Api.IntegrationTests.Startup;

public sealed class StartupValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_Startup_Without_Access_Key(string accessKey)
    {
        using var factory = new EmojiWaterfallWebApplicationFactory().WithWebHostBuilder(b =>
            b.UseSetting("EmojiApi:AccessKey", accessKey)
        );

        var ex = Record.Exception(() => factory.CreateClient());

        var validation = Assert.IsType<OptionsValidationException>(ex?.GetBaseException());
        Assert.Contains("AccessKey", validation.Message);
    }
}
