using FAP.Application.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fap.Client.WinUI.Services;

public sealed class WinUiMessageService : IMessageService
{
    private readonly Window _window;

    public WinUiMessageService(Window window)
    {
        _window = window;
    }

    public void ShowMessage(string message) => _ = ShowAsync(message, "FAP");
    public void ShowWarning(string message) => _ = ShowAsync(message, "Warning");
    public void ShowError(string message) => _ = ShowAsync(message, "Error");

    private async Task ShowAsync(string message, string title)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = _window.Content?.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
