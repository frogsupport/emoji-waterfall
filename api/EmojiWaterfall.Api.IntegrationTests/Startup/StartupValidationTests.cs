using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        using var started = new ManualResetEventSlim();
        IHost? host = null;
        var entryThreadId = 0;

        // Catch the host that the entry point builds on its own thread, so the
        // test can stop it if startup wrongly succeeds (app.Run() never returns).
        using var subscription = DiagnosticListener.AllListeners.Subscribe(
            new HostObserver(
                () => entryThreadId,
                h =>
                {
                    host = h;
                    h.Services.GetRequiredService<IHostApplicationLifetime>()
                        .ApplicationStarted.Register(started.Set);
                }
            )
        );

        var run = Task.Run(() =>
        {
            entryThreadId = Environment.CurrentManagedThreadId;
            return entryPoint.Invoke(
                null,
                [
                    new[]
                    {
                        "--environment=Testing",
                        "--urls=http://127.0.0.1:0",
                        $"--EmojiApi:AccessKey={accessKey}",
                    },
                ]
            );
        });

        // Wait for the entry point to throw, or for the app to report that it started.
        var finished =
            WaitHandle.WaitAny(
                [((IAsyncResult)run).AsyncWaitHandle, started.WaitHandle],
                TimeSpan.FromSeconds(30)
            ) == 0;

        if (!finished)
        {
            // Startup wrongly succeeded. Stop the host, then fail the test.
            host?.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            run.Wait(TimeSpan.FromSeconds(10));
            Assert.Fail("The app started without an access key. Startup must fail.");
        }

        var ex = Record.Exception(() => run.GetAwaiter().GetResult());

        var targetInvocation = Assert.IsType<TargetInvocationException>(ex);
        var validation = Assert.IsType<OptionsValidationException>(targetInvocation.InnerException);
        Assert.Contains("AccessKey", validation.Message);
    }

    private sealed class HostObserver(Func<int> entryThreadId, Action<IHost> onHostBuilt)
        : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.Extensions.Hosting")
            {
                listener.Subscribe(
                    new EventObserver(entryThreadId, onHostBuilt)
                );
            }
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }

    private sealed class EventObserver(Func<int> entryThreadId, Action<IHost> onHostBuilt)
        : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> evt)
        {
            // Events from other tests run on other threads. Ignore them.
            if (
                evt.Key == "HostBuilt"
                && evt.Value is IHost host
                && Environment.CurrentManagedThreadId == entryThreadId()
            )
            {
                onHostBuilt(host);
            }
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }
}
