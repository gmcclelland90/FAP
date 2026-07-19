using FAP.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fap.Client.WinUI.Services;

public sealed class WinUiGettingStartedUi : IGettingStartedUi
{
    private readonly IServiceProvider _services;

    public WinUiGettingStartedUi(IServiceProvider services)
    {
        _services = services;
    }

    public void ShowGettingStarted()
    {
        var window = _services.GetRequiredService<MainWindow>();
        var root = window.Content as FrameworkElement;
        if (root is null)
            return;

        if (root.XamlRoot != null)
        {
            _ = ShowAsync(CreateDialog(root.XamlRoot), window);
            return;
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            root.Loaded -= OnLoaded;
            if (root.XamlRoot != null)
                _ = ShowAsync(CreateDialog(root.XamlRoot), window);
        }

        root.Loaded += OnLoaded;
    }

    private static ContentDialog CreateDialog(XamlRoot xamlRoot)
    {
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(4, 8, 4, 0) };
        body.Children.Add(Step("1. Identity", "Set your nickname and avatar in Settings so peers recognize you."));
        body.Children.Add(Step("2. Shares", "Add folders under Shares to publish files on the LAN."));
        body.Children.Add(Step("3. Mesh", "FAP connects automatically when another peer or overlord is on the network."));
        body.Children.Add(Step("4. Browse & queue", "Open a peer from Home to browse shares; use Queue for downloads."));
        body.Children.Add(Step("5. Chat", "Use Chat for LAN messages and private peer conversations."));

        return new ContentDialog
        {
            Title = "Getting started",
            Content = new ScrollViewer
            {
                Content = body,
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            PrimaryButtonText = "Got it",
            SecondaryButtonText = "Open Settings",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };
    }

    private static UIElement Step(string title, string text)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.85
        });
        return stack;
    }

    private async Task ShowAsync(ContentDialog dialog, MainWindow window)
    {
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
            window.NavigateTag("settings");
    }
}
