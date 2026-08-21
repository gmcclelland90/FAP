using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using FAP.Application.Services;
using FAP.Application.Views;
using FAP.Application.ViewModels;
using Fap.Client.WinUI.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace Fap.Client.WinUI;

public sealed partial class MainWindow : Window, IMainWindow, IShellNavigation
{
    private MainWindowViewModel? _viewModel;
    private AppWindow? _appWindow;
    private bool _shellReady;
    private bool _pendingHomeNavigation;
    private string? _pendingChatPeerId;
    private string _currentTag = "home";
    private string _previousTag = "home";
    private BrowsePage? _browsePage;
    private HomePage? _homePage;
    private ChatPage? _chatPage;
    private IChatSession? _chatSession;
    private readonly Dictionary<string, FeatureHostPage> _featurePages = new(StringComparer.Ordinal);

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Title = "FAP";

        ConfigureAppWindow();

        if (Content is FrameworkElement root)
        {
            AutomationProperties.SetAutomationId(root, "shell.root");
            root.Loaded += (_, _) =>
            {
                _shellReady = true;
                BindChatBadges();
                if (_pendingHomeNavigation || _viewModel != null)
                    NavigateHome();
            };
        }
    }

    public Frame ContentFrame => NavFrame;

    public object? DataContext
    {
        get => _viewModel;
        set
        {
            if (_viewModel != null)
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            _viewModel = value as MainWindowViewModel;
            if (_viewModel == null)
                return;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateShellChrome();

            if (_shellReady)
                NavigateHome();
            else
                _pendingHomeNavigation = true;
        }
    }

    public void Show()
    {
        _appWindow?.Show();
        Activate();
    }

    public void Close() => base.Close();

    public void Flash()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = hwnd,
                dwFlags = 3,
                uCount = 3,
                dwTimeout = 0
            };
            FlashWindowEx(ref info);
        }
        catch
        {
            // best-effort
        }
    }

    public void NavigateToBrowse() => NavigateTag("browse");

    public void GoBack() => NavigateTag(string.IsNullOrEmpty(_previousTag) ? "home" : _previousTag);

    public void NavigateTag(string tag)
    {
        SelectNavItem(tag);
        ShowTag(tag);
    }

    public void NavigateToChat(string? peerId = null)
    {
        _pendingChatPeerId = peerId;
        NavigateTag("chat");
        _chatPage?.OpenPeerId(peerId);
        _pendingChatPeerId = null;
    }

    private void ConfigureAppWindow()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.Closing += AppWindow_Closing;

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
                _appWindow.SetIcon(iconPath);
        }
        catch
        {
            // best-effort
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_viewModel == null || _viewModel.AllowClose)
            return;

        args.Cancel = true;
        _viewModel.Visible = false;
        sender.Hide();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (e.PropertyName is nameof(MainWindowViewModel.ShellStatus)
            or nameof(MainWindowViewModel.IsMeshConnected)
            or null or "")
        {
            UpdateShellChrome();
        }

        if (e.PropertyName != nameof(MainWindowViewModel.Visible))
            return;

        if (_viewModel.Visible)
            Show();
        else
            _appWindow?.Hide();
    }

    private void UpdateShellChrome()
    {
        if (_viewModel == null)
            return;

        AppTitleBar.Subtitle = string.IsNullOrWhiteSpace(_viewModel.ShellStatus)
            ? string.Empty
            : _viewModel.ShellStatus;

        var meshUp = _viewModel.IsMeshConnected;
        const string waitTip = "Waiting for the LAN mesh — peers stay empty until connected.";
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>()
                     .Concat(NavView.FooterMenuItems.OfType<NavigationViewItem>()))
        {
            var tag = item.Tag as string;
            if (tag is "browse" or "chat" or "search" or "queue" or "compare")
                ToolTipService.SetToolTip(item, meshUp ? null : waitTip);
            else
                ToolTipService.SetToolTip(item, null);
        }
    }

    private void NavigateHome()
    {
        _pendingHomeNavigation = false;
        NavigateTag("home");
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_shellReady)
            return;
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;

        ShowTag(tag);
    }

    private void ShowTag(string tag)
    {
        if (!string.Equals(_currentTag, tag, StringComparison.Ordinal))
        {
            _previousTag = _currentTag;
            _currentTag = tag;
        }

        var page = ResolvePage(tag);
        if (!ReferenceEquals(NavFrame.Content, page))
            NavFrame.Content = page;

        if (tag == "chat" && _pendingChatPeerId != null)
        {
            _chatPage?.OpenPeerId(_pendingChatPeerId);
            _pendingChatPeerId = null;
        }
    }

    private FrameworkElement ResolvePage(string tag)
    {
        switch (tag)
        {
            case "home":
                _homePage ??= new HomePage();
                _homePage.Bind(_viewModel);
                return _homePage;
            case "browse":
            {
                var browse = GetOrCreateBrowsePage();
                browse.BindMain(_viewModel);
                return browse;
            }
            case "chat":
                _chatPage ??= new ChatPage();
                _chatPage.EnsureBound();
                return _chatPage;
            case "search":
            case "queue":
            case "shares":
            case "compare":
            case "settings":
                if (!_featurePages.TryGetValue(tag, out var feature))
                {
                    feature = new FeatureHostPage();
                    feature.Load(tag);
                    _featurePages[tag] = feature;
                }
                else
                {
                    feature.Load(tag);
                }
                return feature;
            default:
                _homePage ??= new HomePage();
                _homePage.Bind(_viewModel);
                return _homePage;
        }
    }

    private void SelectNavItem(string tag)
    {
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>()
                     .Concat(NavView.FooterMenuItems.OfType<NavigationViewItem>()))
        {
            if (item.Tag as string == tag)
            {
                NavView.SelectedItem = item;
                return;
            }
        }
    }

    public BrowsePage GetOrCreateBrowsePage()
    {
        _browsePage ??= new BrowsePage();
        return _browsePage;
    }

    private void BindChatBadges()
    {
        if (_chatSession != null || App.Services is null)
            return;

        _chatSession = App.Services.GetService<IChatSession>();
        if (_chatSession == null)
            return;

        _chatSession.Load();
        _chatSession.UnreadChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateChatNavBadge);
        _chatSession.ThreadsChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateChatNavBadge);
        UpdateChatNavBadge();
    }

    private void UpdateChatNavBadge()
    {
        var total = _chatSession?.TotalUnread ?? 0;
        if (total <= 0)
        {
            ChatNavItem.InfoBadge = null;
            return;
        }

        var badge = new InfoBadge { Value = Math.Min(total, 99) };
        if (Application.Current.Resources.TryGetValue("AttentionInfoBadgeStyle", out var style) && style is Style s)
            badge.Style = s;
        ChatNavItem.InfoBadge = badge;
    }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }
}
