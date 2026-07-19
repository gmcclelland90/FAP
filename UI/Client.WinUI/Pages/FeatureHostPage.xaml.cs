using FAP.Application.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Fap.Client.WinUI.Pages;

public sealed partial class FeatureHostPage : Page
{
    private string? _loadedTag;

    public FeatureHostPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
    }

    public void Load(string tag)
    {
        if (App.Services is null)
            return;
        if (string.Equals(_loadedTag, tag, StringComparison.Ordinal) && Host.Content != null)
            return;

        object? content = null;
        switch (tag)
        {
            case "search":
            {
                var c = App.Services.GetRequiredService<SearchController>();
                c.Initalize();
                content = c.ViewModel.View;
                break;
            }
            case "queue":
            {
                var c = App.Services.GetRequiredService<DownloadQueueController>();
                c.Initalise();
                content = c.ViewModel.View;
                break;
            }
            case "shares":
            {
                var c = App.Services.GetRequiredService<SharesController>();
                c.Initalise();
                content = c.ViewModel.View;
                break;
            }
            case "compare":
            {
                var c = App.Services.GetRequiredService<CompareController>();
                content = c.Initalise().View;
                break;
            }
            case "settings":
            {
                var c = App.Services.GetRequiredService<SettingsController>();
                c.Initaize();
                content = c.ViewModel.View;
                break;
            }
        }

        Host.Content = content;
        _loadedTag = tag;
    }
}
