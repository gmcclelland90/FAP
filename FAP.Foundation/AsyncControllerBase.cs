using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace Fap.Foundation
{
    public abstract class AsyncControllerBase
    {
        private BackgroundWorker worker = new BackgroundWorker();
        private Queue<AsyncOperation> operations = new Queue<AsyncOperation>();

        public delegate void AsyncControllerJobComplete();
        public event AsyncControllerJobComplete AsyncControllerJobCompleteHandler;

        public int JobCount
        {
            get
            {
                lock (worker)
                {
                    return operations.Count;
                }
            }
        }

        public AsyncControllerBase()
        {
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            AsyncControllerJobCompleteHandler?.Invoke();
            lock (worker)
            {
                if (operations.Count > 0 && !worker.IsBusy)
                    worker.RunWorkerAsync();
            }
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            AsyncOperation op = null;
            lock (worker)
            {
                if (operations.Count > 0)
                    op = operations.Dequeue();
            }
            if (null != op)
            {
                if (op.Command.CanExecute(op.Object))
                    op.Command.Execute(op.Object);
                e.Result = op;
            }
        }

        protected void QueueWork(ICommand command)
        {
            QueueWork(command, null);
        }

        protected void QueueWork(ICommand command, object param)
        {
            QueueWork(command, param, null);
        }

        protected void QueueWork(ICommand command, object param, ICommand completed)
        {
            lock (worker)
            {
                operations.Enqueue(new AsyncOperation() { Command = command, Object = param });
                if (!worker.IsBusy)
                    worker.RunWorkerAsync();
            }
        }
    }
}
