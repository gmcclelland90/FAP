using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FAP.Application.Views;
using Fap.Foundation;
using Fap.Foundation.Threading;

namespace FAP.Application.ViewModels
{
    public abstract class ViewModelBase<TView> : ObservableObject where TView : IView
    {
        private readonly TView view;

        protected ViewModelBase(TView view)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));

            var ui = SafeObservableStatic.UiDispatcher;
            if (ui != null && !ui.CheckAccess())
                ui.Invoke(() => this.view.DataContext = this);
            else if (SynchronizationContext.Current != null)
                SynchronizationContext.Current.Post(_ => this.view.DataContext = this, null);
            else
                this.view.DataContext = this;
        }

        public object View => view;

        protected TView ViewCore => view;
    }
}
