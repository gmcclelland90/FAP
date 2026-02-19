using System.Windows;
using FAP.Application.Services;

namespace Fap.Presentation.Services
{
    public class WpfMessageService : IMessageService
    {
        private static string AppName => "FAP";

        public void ShowMessage(string message)
        {
            System.Windows.MessageBox.Show(message, AppName, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public void ShowWarning(string message)
        {
            System.Windows.MessageBox.Show(message, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message)
        {
            System.Windows.MessageBox.Show(message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
