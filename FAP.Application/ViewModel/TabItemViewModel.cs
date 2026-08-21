using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FAP.Application.ViewModels
{
    public class TabItemViewModel : INotifyPropertyChanged
    {
        private string _title = null!;
        private object _content = null!;
        private bool _isSelected;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public object Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TabItemViewModel(string title, object content)
        {
            Title = title;
            Content = content;
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