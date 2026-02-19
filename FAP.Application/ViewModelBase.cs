using System;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FAP.Application.Views;

namespace FAP.Application.ViewModels
{
    public abstract class ViewModelBase<TView> : ObservableObject where TView : IView
    {
        private readonly TView view;

        protected ViewModelBase(TView view)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));

            if (SynchronizationContext.Current is DispatcherSynchronizationContext)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(() => view.DataContext = this);
            }
            else
            {
                view.DataContext = this;
            }
        }

        public object View => view;

        protected TView ViewCore => view;
    }
}
