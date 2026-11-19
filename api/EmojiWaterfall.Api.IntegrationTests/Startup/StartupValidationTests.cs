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
    public async Task Should_Fail_Startup_Without_Access_Key(string accessKey)
    {
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IHost? host = null;
        var entryThreadId = 0;

        // Catch the host that the entry point builds on its own thread, so the
        // test can stop it if startup wrongly succeeds (app.Run() never returns).
        using var observer = new HostObserver(
                () => entryThreadId,
                h =>
                {
                    host = h;
                    h.Services.GetRequiredService<IHostApplicationLifetime>()
                        .ApplicationStarted.Register(() => started.TrySetResult());
                }
            );
        using var subscription = DiagnosticListener.AllListeners.Subscribe(observer);

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
        var first = await Task.WhenAny(run, started.Task, Task.Delay(TimeSpan.FromSeconds(30)));

        if (first != run)
        {
            // Startup did not fail. Stop the host if there is one, then fail the test.
            host?.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));

            if (first == started.Task)
            {
                Assert.Fail("The app started without an access key. Startup must fail.");
            }

            Assert.Fail("The entry point did not fail or start within 30 seconds.");
        }

        var ex = await Record.ExceptionAsync(() => run);

        var targetInvocation = Assert.IsType<TargetInvocationException>(ex);
        var validation = Assert.IsType<OptionsValidationException>(targetInvocation.InnerException);
        Assert.Contains("AccessKey", validation.Message);
    }

    private sealed class HostObserver(Func<int> entryThreadId, Action<IHost> onHostBuilt)
        : IObserver<DiagnosticListener>,
            IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];
        private bool _disposed;

        // Dispose the inner subscriptions too. They stay on the process-wide
        // listener otherwise, and would fire for hosts built by later tests.
        public void Dispose()
        {
            lock (_subscriptions)
            {
                _disposed = true;
                _subscriptions.ForEach(s => s.Dispose());
                _subscriptions.Clear();
            }
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.Extensions.Hosting")
            {
                lock (_subscriptions)
                {
                    if (!_disposed)
                    {
                        _subscriptions.Add(
                            listener.Subscribe(new EventObserver(entryThreadId, onHostBuilt))
                        );
                    }
                }
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
