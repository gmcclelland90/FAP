using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using FAP.Application.Controllers;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Services;
using Fap.Client.WinUI.Views;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Fap.Client.WinUI.Pages;

public sealed partial class BrowsePage : Page
{
    private MainWindowViewModel? _mainVm;
    private INotifyCollectionChanged? _peersSource;
    private readonly ObservableCollection<PeerPickRow> _peerRows = new();

    public BrowsePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        PeerPicker.ItemsSource = _peerRows;
        PeerPicker.ItemTemplate = ListTemplates.PeerRow();
        Loaded += (_, _) => RefreshEmptyChrome();
        UpdateEmptyState();
    }

    public void BindMain(MainWindowViewModel? vm)
    {
        if (ReferenceEquals(_mainVm, vm) && vm != null)
        {
            RefreshEmptyChrome();
            return;
        }

        if (_mainVm != null)
            _mainVm.PropertyChanged -= MainVm_PropertyChanged;

        DetachPeers();
        _mainVm = vm;
        if (_mainVm != null)
        {
            _mainVm.PropertyChanged += MainVm_PropertyChanged;
            AttachPeers(_mainVm.Peers);
        }

        RefreshEmptyChrome();
    }

    public void OpenPeer(Node peer)
    {
        if (App.Services is null || peer is null)
            return;

        // Reuse existing tab for the same peer id.
        foreach (var item in PeerTabs.TabItems.OfType<TabViewItem>())
        {
            if (item.Tag is string id && id == peer.ID)
            {
                PeerTabs.SelectedItem = item;
                UpdateEmptyState();
                return;
            }
        }

        var model = App.Services.GetRequiredService<Model>();
        var browserView = App.Services.GetRequiredService<FAP.Application.Views.IBrowserView>();
        var browserViewModel = new BrowserViewModel(browserView);
        var shareInfo = App.Services.GetRequiredService<ShareInfoService>();

        var properNode = ClonePeer(peer, model);
        var controller = new BrowserController(
            browserViewModel,
            model,
            properNode,
            shareInfo,
            App.Services.GetRequiredService<System.Net.Http.IHttpClientFactory>(),
            App.Services.GetRequiredService<ILogger<ModernHttpClient>>());
        controller.Initalise();

        var tab = new TabViewItem
        {
            Header = string.IsNullOrWhiteSpace(properNode.Nickname) ? properNode.Host : properNode.Nickname,
            Content = browserViewModel.View as UIElement ?? new TextBlock { Text = properNode.Nickname },
            Tag = properNode.ID,
            IsClosable = true
        };
        PeerTabs.TabItems.Add(tab);
        PeerTabs.SelectedItem = tab;
        UpdateEmptyState();
    }

    private void PeerTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        sender.TabItems.Remove(args.Tab);
        UpdateEmptyState();
    }

    private void GoHomeButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.Services?.GetService<MainWindow>() is { } window)
            window.NavigateTag("home");
    }

    private void RefreshPeersButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainVm?.Peers != null)
            AttachPeers(_mainVm.Peers);
        else if (App.Services?.GetService<MainWindowViewModel>() is { } vm)
            BindMain(vm);
        else
            RefreshEmptyChrome();
    }

    private void PeerPicker_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PeerPickRow row)
            OpenPeer(row.Node);
    }

    private void MainVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.Peers)
            or nameof(MainWindowViewModel.IsMeshConnected)
            or nameof(MainWindowViewModel.ShellStatus)
            or null or "")
        {
            if (e.PropertyName == nameof(MainWindowViewModel.Peers))
                AttachPeers(_mainVm?.Peers);
            else
                RefreshEmptyChrome();
        }
    }

    private void AttachPeers(IEnumerable<Node>? source)
    {
        DetachPeers();
        RebuildPeers(source);
        if (source is INotifyCollectionChanged ncc)
        {
            _peersSource = ncc;
            _peersSource.CollectionChanged += Peers_CollectionChanged;
        }
    }

    private void DetachPeers()
    {
        if (_peersSource != null)
        {
            _peersSource.CollectionChanged -= Peers_CollectionChanged;
            _peersSource = null;
        }
    }

    private void Peers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        void Apply() => RebuildPeers(_mainVm?.Peers);
        if (DispatcherQueue.HasThreadAccess)
            Apply();
        else
            DispatcherQueue.TryEnqueue(Apply);
    }

    private void RebuildPeers(IEnumerable<Node>? source)
    {
        _peerRows.Clear();
        if (source == null) return;
        foreach (var node in source)
            _peerRows.Add(PeerPickRow.From(node));
        RefreshEmptyChrome();
    }

    private void UpdateEmptyState()
    {
        var hasTabs = PeerTabs.TabItems.Count > 0;
        PeerTabs.Visibility = hasTabs ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasTabs ? Visibility.Collapsed : Visibility.Visible;
        RefreshEmptyChrome();
    }

    private void RefreshEmptyChrome()
    {
        var peerCount = _peerRows.Count;
        var meshUp = _mainVm?.IsMeshConnected == true;
        if (PeerTabs.TabItems.Count > 0)
        {
            var header = (PeerTabs.SelectedItem as TabViewItem)?.Header?.ToString();
            BrowseStatus.Text = string.IsNullOrWhiteSpace(header)
                ? "Browsing peer shares"
                : $"Browsing {header}";
        }
        else if (!meshUp)
        {
            BrowseStatus.Text = "No peer open · waiting for mesh";
        }
        else if (peerCount == 0)
        {
            BrowseStatus.Text = "No peer open · 0 online";
        }
        else
        {
            BrowseStatus.Text = peerCount == 1
                ? "No peer open · 1 online"
                : $"No peer open · {peerCount} online";
        }

        var emptyPeers = peerCount == 0;
        PeerPickerEmpty.Visibility = emptyPeers ? Visibility.Visible : Visibility.Collapsed;
        PeerPicker.Visibility = emptyPeers ? Visibility.Collapsed : Visibility.Visible;
    }

    private static Node ClonePeer(Node rc, Model model)
    {
        var properNode = new Node
        {
            Host = !string.IsNullOrEmpty(rc.Host) ? rc.Host : model.LocalNode.Host,
            ID = !string.IsNullOrEmpty(rc.ID) ? rc.ID : model.LocalNode.ID,
            Nickname = !string.IsNullOrEmpty(rc.Nickname) ? rc.Nickname : model.Nickname,
            NodeType = rc.NodeType,
            Online = rc.Online
        };
        foreach (var kvp in rc.Data)
            properNode.SetData(kvp.Key, kvp.Value);
        return properNode;
    }

    private sealed class PeerPickRow
    {
        public required Node Node { get; init; }
        public required string Title { get; init; }
        public required string DisplayName { get; init; }
        public required string Subtitle { get; init; }
        public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? ProfilePicture { get; init; }

        public static PeerPickRow From(Node n)
        {
            var title = string.IsNullOrWhiteSpace(n.Nickname) ? n.Host : n.Nickname;
            var subtitle = $"{n.Host} · {Utility.FormatBytes(n.ShareSize)} · {n.FileCount:N0} files";
            return new PeerPickRow
            {
                Node = n,
                Title = title,
                DisplayName = string.IsNullOrWhiteSpace(title) ? "Peer" : title,
                Subtitle = subtitle,
                ProfilePicture = null
            };
        }
    }
}
