using System.Text;
using FAP.Application;
using FAP.Application.Controllers;
using FAP.Application.DependencyInjection;
using FAP.Application.Services;
using FAP.Application.Views;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using FAP.Domain.Verbs;
using Fap.Client.WinUI.Services;
using Fap.Client.WinUI.Views;
using Fap.Foundation;
using Fap.Foundation.Hosting;
using Fap.Foundation.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI.Popups;

namespace Fap.Client.WinUI;

public partial class App : Application
{
    private MainWindow? _window;
    private IHost? _host;
    public static IServiceProvider? Services { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCrash("UnhandledException", e.Exception);
        e.Handled = true;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            SafeObservableStatic.UiDispatcher = new WinUiDispatcher(queue);
            SafeObservingCollectionManager.Start();

            if (!Compose())
            {
                LogCrash("Compose", new InvalidOperationException("DI composition failed"));
                Exit();
                return;
            }

            var core = Services!.GetRequiredService<ApplicationCore>();
            if (!core.CheckSingleInstance())
            {
                ForwardUrlArgsAndExit();
                return;
            }

            if (!core.Load(false))
            {
                LogCrash("Load", new InvalidOperationException("ApplicationCore.Load returned false (interface selection cancelled?)"));
                Exit();
                return;
            }

            // Create and activate the shell before StartGUI so Frame navigation is valid.
            _window = Services.GetRequiredService<MainWindow>();
            _window.Activate();

            HandleStartupUrlArgs(core);
            core.StartClient();
            core.StartGUI(true);

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            _window.DataContext = mainVm;
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched", ex);
            // Keep process alive long enough for crash log; avoid FailFast via rethrow.
        }
    }

    private bool Compose()
    {
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            var services = builder.Services;
            services.AddSingleton<IUiDispatcher>(_ => SafeObservableStatic.UiDispatcher!);
            services.AddSingleton<IAppLifetime, WinUiAppLifetime>();
            services.AddFapCore(builder.Configuration);
            services.AddFapClient();

            services.AddSingleton<MainWindow>();
            services.AddSingleton<IMainWindow>(sp => sp.GetRequiredService<MainWindow>());
            services.AddSingleton<IShellNavigation>(sp => sp.GetRequiredService<MainWindow>());
            services.AddSingleton<IGettingStartedUi, WinUiGettingStartedUi>();
            services.AddSingleton<IBrowseSessionHost, BrowseSessionHost>();
            services.AddTransient<IMessageService>(sp => new WinUiMessageService(sp.GetRequiredService<MainWindow>()));
            services.AddTransient<IBrowserView, BrowserView>();
            services.AddTransient<ISearchView, SearchView>();
            services.AddTransient<ISharesView, SharesView>();
            services.AddTransient<ISettingsView, SettingsView>();
            services.AddTransient<IDownloadQueue, DownloadQueueView>();
            services.AddTransient<ICompareView, CompareView>();
            services.AddTransient<IConverstationView, ConversationView>();
            services.AddTransient<IMessageBoxView, MessageBoxView>();
            services.AddTransient<IInterfaceSelectionView, InterfaceSelectionView>();
            services.AddTransient<IQuery>(sp => new QueryService(sp.GetRequiredService<MainWindow>()));
            services.AddSingleton<ITrayIconView, TrayIconView>();

            _host = builder.Build();
            Services = _host.Services;
            return true;
        }
        catch (Exception ex)
        {
            LogCrash("Compose", ex);
            return false;
        }
    }

    private static void HandleStartupUrlArgs(ApplicationCore core)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "-url", StringComparison.OrdinalIgnoreCase))
            {
                core.AddDownloadUrlWhenConnected(args[i + 1]);
                break;
            }
        }
    }

    private static void ForwardUrlArgsAndExit()
    {
        var args = Environment.GetCommandLineArgs();
        string? url = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "-url", StringComparison.OrdinalIgnoreCase))
            {
                url = args[i + 1];
                break;
            }
        }

        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                var model = new Model();
                model.Load();
                var client = new FAP.Domain.Net.Client(model.LocalNode);
                var verb = new AddDownload { URL = url };
                if (client.Execute(verb, model.LocalNode))
                {
                    Environment.Exit(0);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogCrash("ForwardUrl", ex);
            }

            _ = ShowFatalAsync("Failed to add download via RPC.");
            Environment.Exit(1);
            return;
        }

        _ = ShowFatalAsync("An instance of FAP is already running.");
        Environment.Exit(1);
    }

    private static async Task ShowFatalAsync(string message)
    {
        try
        {
            var dialog = new MessageDialog(message, "FAP");
            await dialog.ShowAsync();
        }
        catch
        {
            // headless
        }
    }

    private static void LogCrash(string stage, Exception? ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "fap-winui-crash.log");
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:O}] {stage}");
            sb.AppendLine(ex?.ToString() ?? "(null)");
            sb.AppendLine();
            File.AppendAllText(path, sb.ToString());
        }
        catch
        {
            // ignore logging failures
        }
    }
}
