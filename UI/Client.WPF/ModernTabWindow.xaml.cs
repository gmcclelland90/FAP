using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FAP.Application.ViewModels;

namespace Fap.Presentation
{
    public partial class ModernTabWindow : Window
    {
        public ModernTabWindow()
        {
            InitializeComponent();
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TabItemViewModel tab)
            {
                var viewModel = DataContext as ModernTabWindowViewModel;
                viewModel?.RemoveTab(tab);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var viewModel = DataContext as ModernTabWindowViewModel;
            viewModel?.Close();
            base.OnClosing(e);
        }
    }
} 