using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Fap.Foundation.Threading;

namespace Fap.Presentation.Services
{
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        private readonly Dispatcher _dispatcher;

        public WpfUiDispatcher(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public bool CheckAccess() => _dispatcher.CheckAccess();

        public void Invoke(Action action) => _dispatcher.Invoke(action);

        public Task InvokeAsync(Action action) => _dispatcher.InvokeAsync(action).Task;

        public Task InvokeAsync(Func<Task> action)
        {
            if (CheckAccess())
                return action();

            var tcs = new TaskCompletionSource();
            _ = _dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await action().ConfigureAwait(true);
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }
    }
}
