using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

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
                    await Task.Run(() =>
                    {
                        if (op.Command.CanExecute(op.Object))
                            op.Command.Execute(op.Object);
                    }, cancellationTokenSource.Token);
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

        protected void QueueWork(ICommand command)
        {
            QueueWork(command, null, null);
        }

        protected void QueueWork(ICommand command, object? param)
        {
            QueueWork(command, param, null);
        }

        protected void QueueWork(ICommand command, object? param, ICommand? completed)
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
