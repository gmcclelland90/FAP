using System.Threading;
using FAP.Application.Controllers;
using FAP.Application.DependencyInjection;
using FAP.Application.Services;
using FAP.Application.ViewModels;
using FAP.Application.Views;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using FAP.Network.Server;
using FAP.Shared.Services;
using Fap.Foundation.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FAP.IntegrationTests;

/// <summary>
/// Verifies AddFapCore + AddFapClient compose without missing registrations when UI stubs are supplied.
/// </summary>
public class ShippingCompositionTests
{
    private sealed class StubView : IMainWindow, IDownloadQueue, ISharesView, ISearchView, ISettingsView, ICompareView, IBrowserView, IPopupWindow, IConverstationView, IInterfaceSelectionView
    {
        public object DataContext { get; set; } = null!;
        public bool? ShowDialog() => true;
        public void Show() { }
        public void Close() { }
        public void Flash() { }
        public void FlashIfNotActive() { }
    }

    private sealed class StubQuery : IQuery
    {
        public bool SelectFolder(out string result) { result = string.Empty; return false; }
        public bool SelectFile(out string result) { result = string.Empty; return false; }
        public bool SelectImageFile(out string result) { result = string.Empty; return false; }
    }

    private sealed class StubMessageService : IMessageService
    {
        public void ShowMessage(string message) { }
        public void ShowWarning(string message) { }
        public void ShowError(string message) { }
    }

    private sealed class StubShellNavigation : IShellNavigation
    {
        public void GoBack() { }
        public void NavigateTag(string tag) { }
        public void NavigateToChat(string? peerId = null) { }
    }

    private sealed class StubGettingStarted : IGettingStartedUi
    {
        public void ShowGettingStarted() { }
    }

    [Fact]
    public void AddFapCore_and_AddFapClient_resolve_core_services()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddOptions();
        services.Configure<FapListenOptions>(o => { o.Address = "127.0.0.1"; o.Port = 40; });
        services.AddFapCore();
        services.AddFapClient();

        var stub = new StubView();
        services.AddSingleton<IUiDispatcher>(_ => new SynchronizationContextUiDispatcher(SynchronizationContext.Current ?? new SynchronizationContext()));
        services.AddSingleton<IMainWindow>(_ => stub);
        services.AddSingleton<IDownloadQueue>(_ => stub);
        services.AddSingleton<ISharesView>(_ => stub);
        services.AddSingleton<ISearchView>(_ => stub);
        services.AddSingleton<ISettingsView>(_ => stub);
        services.AddSingleton<ICompareView>(_ => stub);
        services.AddSingleton<IBrowserView>(_ => stub);
        services.AddTransient<IPopupWindow>(_ => stub);
        services.AddSingleton<IShellNavigation, StubShellNavigation>();
        services.AddSingleton<IGettingStartedUi, StubGettingStarted>();
        services.AddSingleton<IConverstationView>(_ => stub);
        services.AddSingleton<IQuery, StubQuery>();
        services.AddSingleton<IMessageService, StubMessageService>();
        services.AddSingleton<IInterfaceSelectionView>(_ => stub);
        // Override ConversationController with stub so Listener composition stays UI-free.
        services.AddSingleton<IConversationController, TestConversationController>();

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<Model>());
        Assert.NotNull(sp.GetRequiredService<ShareInfoService>());
        Assert.NotNull(sp.GetRequiredService<IListenerServiceFactory>());
        Assert.NotNull(sp.GetRequiredService<IBrowsePageHtmlRenderer>());
        Assert.NotNull(sp.GetRequiredService<MainWindowViewModel>());
        System.Console.WriteLine("[Composition] AddFapCore/AddFapClient ok");
    }
}
