using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fap.Foundation
{
    public abstract class AsyncControllerBase
    {
        private readonly Queue<AsyncOperation> operations = new Queue<AsyncOperation>();
        private readonly object lockObject = new object();
        private readonly SynchronizationContext syncContext;
        private volatile bool isProcessing;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public delegate void AsyncControllerJobComplete();
        public event AsyncControllerJobComplete? AsyncControllerJobCompleteHandler;

        public int JobCount
        {
            get
            {
                lock (lockObject)
                {
                    return operations.Count;
                }
            }
        }

        public AsyncControllerBase()
        {
            syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                AsyncOperation op;
                lock (lockObject)
                {
                    if (operations.Count == 0)
                    {
                        isProcessing = false;
                        return;
                    }
                    op = operations.Dequeue();
                }

                try
                {
                    await Task.Run(() => op.Command(op.Object), cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    lock (lockObject)
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                }

                syncContext.Post(_ =>
                {
                    AsyncControllerJobCompleteHandler?.Invoke();
                }, null);

                lock (lockObject)
                {
                    if (operations.Count == 0)
                    {
                        isProcessing = false;
                        return;
                    }
                }
            }
        }

        protected void QueueWork(Action<object?> command)
        {
            QueueWork(command, null);
        }

        protected void QueueWork(Action<object?> command, object? param)
        {
            lock (lockObject)
            {
                operations.Enqueue(new AsyncOperation() { Command = command, Object = param });
                if (!isProcessing)
                {
                    isProcessing = true;
                    _ = ProcessQueueAsync();
                }
            }
        }
    }
}
