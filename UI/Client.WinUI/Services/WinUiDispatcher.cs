using Fap.Foundation.Threading;
using Microsoft.UI.Dispatching;

namespace Fap.Client.WinUI.Services;

public sealed class WinUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _queue;

    public WinUiDispatcher(DispatcherQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public bool CheckAccess() => _queue.HasThreadAccess;

    public void Invoke(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        var done = new ManualResetEventSlim(false);
        Exception? error = null;
        _queue.TryEnqueue(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
            finally { done.Set(); }
        });
        done.Wait();
        if (error != null) throw error;
    }

    public Task InvokeAsync(Action action)
    {
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        _queue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public Task InvokeAsync(Func<Task> action)
    {
        if (CheckAccess())
            return action();

        var tcs = new TaskCompletionSource();
        _queue.TryEnqueue(async () =>
        {
            try
            {
                await action().ConfigureAwait(true);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
