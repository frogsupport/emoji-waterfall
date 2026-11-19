using System.Reflection;
using Microsoft.Extensions.Options;

namespace EmojiWaterfall.Api.IntegrationTests.Startup;

public sealed class StartupValidationTests
{
    // This test runs the real entry point of the API, not WebApplicationFactory.
    // When startup fails, the factory reads the host services on the test thread
    // while the entry point thread already disposed them. That race throws an
    // ObjectDisposedException in place of the validation error.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_Startup_Without_Access_Key(string accessKey)
    {
        var entryPoint = typeof(Program).Assembly.EntryPoint!;

        // If validation regresses, startup succeeds and Invoke never returns.
        // Run it on a pool thread (a background thread, so it cannot keep the
        // test process alive) and fail the test when it does not throw in time.
        var run = Task.Run(() =>
            entryPoint.Invoke(
                null,
                [
                    new[]
                    {
                        "--environment=Testing",
                        "--urls=http://127.0.0.1:0",
                        $"--EmojiApi:AccessKey={accessKey}",
                    },
                ]
            )
        );

        var finished = ((IAsyncResult)run).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
        Assert.True(finished, "The app started without an access key. Startup must fail.");

        var ex = Record.Exception(() => run.GetAwaiter().GetResult());

        var targetInvocation = Assert.IsType<TargetInvocationException>(ex);
        var validation = Assert.IsType<OptionsValidationException>(targetInvocation.InnerException);
        Assert.Contains("AccessKey", validation.Message);
    }
}
