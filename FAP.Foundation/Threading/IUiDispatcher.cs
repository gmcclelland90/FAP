using System;
using System.Threading.Tasks;

namespace Fap.Foundation.Threading
{
    /// <summary>
    /// UI-thread marshalling seam. WPF and WinUI supply adapters; Domain/Application stay UI-free.
    /// </summary>
    public interface IUiDispatcher
    {
        bool CheckAccess();
        void Invoke(Action action);
        Task InvokeAsync(Action action);
        Task InvokeAsync(Func<Task> action);
    }
}
