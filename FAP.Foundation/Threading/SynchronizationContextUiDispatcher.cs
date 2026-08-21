using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fap.Foundation.Threading
{
    public sealed class SynchronizationContextUiDispatcher : IUiDispatcher
    {
        private readonly SynchronizationContext _context;

        public SynchronizationContextUiDispatcher(SynchronizationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public bool CheckAccess() => SynchronizationContext.Current == _context;

        public void Invoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (CheckAccess())
            {
                action();
                return;
            }

            _context.Send(_ => action(), null);
        }

        public Task InvokeAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource();
            _context.Post(_ =>
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
            }, null);
            return tcs.Task;
        }

        public Task InvokeAsync(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (CheckAccess())
                return action();

            var tcs = new TaskCompletionSource();
            _context.Post(async _ =>
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
            }, null);
            return tcs.Task;
        }
    }
}
