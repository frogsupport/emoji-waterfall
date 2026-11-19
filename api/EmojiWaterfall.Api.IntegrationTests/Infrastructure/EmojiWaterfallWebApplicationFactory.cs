using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EmojiWaterfall.Api.IntegrationTests.Infrastructure;

public class EmojiWaterfallWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // A fake key, so startup validation passes in tests.
        builder.UseSetting("EmojiApi:AccessKey", "test-access-key");

        // Tests must not call emoji-api.com, so skip the startup cache fill.
        builder.ConfigureServices(services =>
        {
            var startupFetch = services.Single(d =>
                d.ImplementationType == typeof(StartupFetchService)
            );
            services.Remove(startupFetch);
        });
    }
}
