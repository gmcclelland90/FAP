using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using Fap.Client.WinUI.Views;
using Fap.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Streams;

namespace Fap.Client.WinUI.Pages;

public sealed partial class HomePage : Page
{
    private MainWindowViewModel? _vm;
    private INotifyCollectionChanged? _peersSource;
    private INotifyCollectionChanged? _sessionsSource;
    private readonly ObservableCollection<PeerRowModel> _peerRows = new();

    public HomePage()
    {
        InitializeComponent();
        PeersList.ItemsSource = _peerRows;
        PeersList.ItemTemplate = ListTemplates.PeerRow();
        _peerRows.CollectionChanged += (_, _) => UpdateEmptyStates();
        UpdateEmptyStates();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Bind(e.Parameter as MainWindowViewModel);
    }

    public void Bind(MainWindowViewModel? vm)
    {
        if (ReferenceEquals(_vm, vm) && vm != null)
        {
            UpdateStatusTexts();
            for (var i = 0; i < _peerRows.Count; i++)
                _peerRows[i] = PeerRowModel.From(_peerRows[i].Node);
            UpdateEmptyStates();
            return;
        }

        if (_vm != null)
            _vm.PropertyChanged -= OnVmChanged;

        DetachPeers();
        DetachSessions();
        _vm = vm;
        DataContext = _vm;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmChanged;
            AttachSessions(_vm.Sessions);
            AttachPeers(_vm.Peers);
            UpdateStatusTexts();
            UpdateEmptyStates();
        }
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.NodeStatus):
            case nameof(MainWindowViewModel.NodeStatusDetail):
            case nameof(MainWindowViewModel.LocalStats):
            case nameof(MainWindowViewModel.GlobalStats):
                UpdateStatusTexts();
                break;
            case nameof(MainWindowViewModel.IsMeshConnected):
                UpdateStatusTexts();
                UpdateEmptyStates();
                break;
            case nameof(MainWindowViewModel.Peers):
                AttachPeers(_vm?.Peers);
                break;
            case nameof(MainWindowViewModel.Sessions):
                AttachSessions(_vm?.Sessions);
                break;
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

        if (source != null)
        {
            foreach (var node in source)
                node.PropertyChanged += Peer_PropertyChanged;
        }
    }

    private void DetachPeers()
    {
        if (_peersSource != null)
        {
            _peersSource.CollectionChanged -= Peers_CollectionChanged;
            _peersSource = null;
        }

        foreach (var row in _peerRows)
            row.Node.PropertyChanged -= Peer_PropertyChanged;
    }

    private void Peers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        void Apply()
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildPeers(_vm?.Peers);
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is Node node)
                    {
                        node.PropertyChanged += Peer_PropertyChanged;
                        _peerRows.Add(PeerRowModel.From(node));
                    }
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is Node node)
                    {
                        node.PropertyChanged -= Peer_PropertyChanged;
                        var row = _peerRows.FirstOrDefault(r => ReferenceEquals(r.Node, node));
                        if (row != null)
                            _peerRows.Remove(row);
                    }
                }
            }
        }

        if (DispatcherQueue.HasThreadAccess)
            Apply();
        else
            DispatcherQueue.TryEnqueue(Apply);
    }

    private void Peer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Node node) return;
        if (e.PropertyName is not (nameof(Node.Nickname) or nameof(Node.Host) or nameof(Node.ShareSize)
            or nameof(Node.FileCount) or nameof(Node.Avatar) or nameof(Node.Description) or null or ""))
            return;

        void Apply()
        {
            var index = _peerRows.ToList().FindIndex(r => ReferenceEquals(r.Node, node));
            if (index < 0) return;
            _peerRows[index] = PeerRowModel.From(node);
        }

        if (DispatcherQueue.HasThreadAccess)
            Apply();
        else
            DispatcherQueue.TryEnqueue(Apply);
    }

    private void RebuildPeers(IEnumerable<Node>? source)
    {
        foreach (var row in _peerRows)
            row.Node.PropertyChanged -= Peer_PropertyChanged;
        _peerRows.Clear();
        if (source == null) return;
        foreach (var node in source)
        {
            node.PropertyChanged += Peer_PropertyChanged;
            _peerRows.Add(PeerRowModel.From(node));
        }
    }

    private void AttachSessions(IEnumerable<TransferSession>? source)
    {
        DetachSessions();
        SessionsList.ItemsSource = source;
        if (source is INotifyCollectionChanged ncc)
        {
            _sessionsSource = ncc;
            _sessionsSource.CollectionChanged += Sessions_CollectionChanged;
        }
        UpdateEmptyStates();
    }

    private void DetachSessions()
    {
        if (_sessionsSource != null)
        {
            _sessionsSource.CollectionChanged -= Sessions_CollectionChanged;
            _sessionsSource = null;
        }
    }

    private void Sessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
            UpdateEmptyStates();
        else
            DispatcherQueue.TryEnqueue(UpdateEmptyStates);
    }

    private void UpdateStatusTexts()
    {
        if (_vm == null) return;
        // When shell already shows Connected · nick, lead Home with mesh role / peer summary.
        var peerCount = _peerRows.Count;
        if (_vm.IsMeshConnected)
        {
            StatusText.Text = peerCount == 0
                ? "Mesh up · waiting for peers"
                : peerCount == 1
                    ? "Mesh up · 1 peer"
                    : $"Mesh up · {peerCount} peers";
        }
        else
        {
            StatusText.Text = _vm.NodeStatus ?? "FAP";
        }

        var local = _vm.LocalStats ?? "";
        var global = _vm.GlobalStats ?? "";
        var idleTransfers = local.Contains("No transfers", StringComparison.OrdinalIgnoreCase)
            && global.Contains("No transfers", StringComparison.OrdinalIgnoreCase);
        LocalStatsText.Text = idleTransfers ? "" : local;
        GlobalStatsText.Text = idleTransfers ? "" : global;
        LocalStatsText.Visibility = string.IsNullOrWhiteSpace(LocalStatsText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
        GlobalStatsText.Visibility = string.IsNullOrWhiteSpace(GlobalStatsText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var detail = _vm.NodeStatusDetail ?? string.Empty;
        StatusDetailText.Text = detail;
        StatusDetailText.Visibility = string.IsNullOrWhiteSpace(detail)
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Single source: IsMeshConnected from ConnectionState (not string parsing).
        var waiting = !_vm.IsMeshConnected;
        StatusInfoBar.IsOpen = waiting;
        if (waiting)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Warning;
            StatusInfoBar.Title = "Waiting to connect";
            StatusInfoBar.Message = "Peers stay empty until the mesh is up.";
        }
        else
        {
            StatusInfoBar.Title = string.Empty;
            StatusInfoBar.Message = string.Empty;
        }
    }

    private void UpdateEmptyStates()
    {
        var peerCount = _peerRows.Count;
        var peersEmpty = peerCount == 0;
        var transfersEmpty = true;
        if (SessionsList.ItemsSource is System.Collections.ICollection coll)
            transfersEmpty = coll.Count == 0;
        else if (SessionsList.ItemsSource is System.Collections.IEnumerable en)
            transfersEmpty = !en.Cast<object>().Any();

        PeersHeader.Text = peersEmpty ? "Peers" : $"Peers · {peerCount}";
        if (PeersEmptyBody != null)
        {
            PeersEmptyBody.Text = _vm?.IsMeshConnected == true
                ? "No other peers on the mesh yet. Select a peer to browse shares when they appear."
                : "Peers show up when the LAN mesh connects. Select a peer to browse shares.";
        }

        // Keep ListViews visible so AutomationIds remain in the UIA tree for tests.
        PeersEmpty.Visibility = peersEmpty ? Visibility.Visible : Visibility.Collapsed;
        TransfersEmpty.Visibility = transfersEmpty ? Visibility.Visible : Visibility.Collapsed;
        PeersEmpty.IsHitTestVisible = peersEmpty;
        TransfersEmpty.IsHitTestVisible = transfersEmpty;
    }

    private void PeersEmptySettings_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        CommandHelpers.TryExecute(_vm.Settings);
    }

    private void OpenQueueLink_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        CommandHelpers.TryExecute(_vm.ViewQueue);
    }

    private void PeersList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_vm == null || e.ClickedItem is not PeerRowModel row)
            return;
        BrowsePeer(row.Node);
    }

    private void PeersList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var node = ResolvePeer(e.OriginalSource) ?? (PeersList.SelectedItem as PeerRowModel)?.Node;
        if (node is null) return;
        BrowsePeer(node);
    }

    private void BrowsePeer(Node node)
    {
        if (_vm == null) return;
        _vm.SelectedClient = node;
        CommandHelpers.TryExecute(_vm.ViewShare, node);
    }

    private void PeersList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var node = ResolvePeer(e.OriginalSource) ?? (PeersList.SelectedItem as PeerRowModel)?.Node;
        if (_vm == null || node is null) return;

        PeersList.SelectedItem = _peerRows.FirstOrDefault(r => ReferenceEquals(r.Node, node));
        _vm.SelectedClient = node;

        var flyout = new MenuFlyout();
        flyout.Items.Add(Menu("Browse shares", () => BrowsePeer(node)));
        flyout.Items.Add(Menu("Web share", () =>
            CommandHelpers.TryExecute(_vm.OpenExternal, "http://" + node.Location)));
        flyout.Items.Add(Menu("Chat", () => CommandHelpers.TryExecute(_vm.Chat, node)));

        if (e.OriginalSource is UIElement el)
            flyout.ShowAt(el, e.GetPosition(el));
        else
            flyout.ShowAt(PeersList);
    }

    private static MenuFlyoutItem Menu(string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => action();
        return item;
    }

    private static Node? ResolvePeer(object? source)
    {
        DependencyObject? current = source as DependencyObject;
        while (current != null)
        {
            if (current is FrameworkElement fe)
            {
                if (fe.DataContext is PeerRowModel row)
                    return row.Node;
                if (fe.DataContext is Node node)
                    return node;
            }
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private sealed class PeerRowModel
    {
        public required Node Node { get; init; }
        public required string Title { get; init; }
        public required string DisplayName { get; init; }
        public required string Subtitle { get; init; }
        public BitmapImage? ProfilePicture { get; init; }

        public static PeerRowModel From(Node n)
        {
            var title = string.IsNullOrWhiteSpace(n.Nickname) ? n.Host : n.Nickname;
            var subtitle = string.IsNullOrWhiteSpace(n.Description)
                ? $"{n.Host} · {Utility.FormatBytes(n.ShareSize)} · {n.FileCount:N0} files"
                : $"{n.Description} · {Utility.FormatBytes(n.ShareSize)} · {n.FileCount:N0} files";

            return new PeerRowModel
            {
                Node = n,
                Title = title,
                DisplayName = string.IsNullOrWhiteSpace(title) ? "Peer" : title,
                Subtitle = subtitle,
                ProfilePicture = DecodeAvatar(n.Avatar)
            };
        }

        private static BitmapImage? DecodeAvatar(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            try
            {
                var bytes = Convert.FromBase64String(base64);
                var bitmap = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                stream.WriteAsync(bytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
                stream.Seek(0);
                bitmap.SetSource(stream);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
