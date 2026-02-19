using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FAP.Application.ViewModels
{
    public class ModernTabWindowViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<TabItemViewModel> _tabs;
        private TabItemViewModel _selectedTab = null!;

        public ModernTabWindowViewModel()
        {
            _tabs = new ObservableCollection<TabItemViewModel>();
        }

        public ObservableCollection<TabItemViewModel> Tabs => _tabs;

        public TabItemViewModel SelectedTab
        {
            get => _selectedTab;
            set => SetProperty(ref _selectedTab, value);
        }

        public void AddTab(string title, object content)
        {
            var tab = new TabItemViewModel(title, content);
            _tabs.Add(tab);
            SelectedTab = tab;
        }

        public void RemoveTab(TabItemViewModel tab)
        {
            if (_tabs.Contains(tab))
            {
                _tabs.Remove(tab);
                
                // If we removed the selected tab, select the last remaining tab
                if (SelectedTab == tab && _tabs.Count > 0)
                {
                    SelectedTab = _tabs[_tabs.Count - 1];
                }
            }
        }

        public void Close()
        {
            // Clean up any resources if needed
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
} 