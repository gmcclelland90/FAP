using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using FAP.Application.ViewModel;
using FAP.Application.ViewModels;
using FAP.Application.Views;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Entities.FileSystem;
using FAP.Application.Services;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Fap.Client.WinUI.Views;

public sealed class BrowserView : WinUiViewBase, IBrowserView
{
    private readonly BreadcrumbBar _crumbs = new();
    private readonly ListView _folders = new() { MinWidth = 220, IsItemClickEnabled = true };
    private readonly ListView _files = new() { IsItemClickEnabled = true };
    private readonly TextBlock _empty = new()
    {
        Text = "No files or folders in this location.",
        Opacity = 0.7,
        Margin = new Thickness(16),
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false
    };
    private readonly TextBlock _status = new();
    private readonly Button _refresh = new() { Content = "Refresh" };
    private readonly Button _download = new() { Content = "Download" };
    private BrowserViewModel? _vm;
    private BrowsingFile? _boundCurrentItem;
    private bool _suppressSelectionSync;

    public BrowserView()
    {
        AutomationProperties.SetAutomationId(this, "browse.view");
        AutomationProperties.SetAutomationId(_folders, "browse.folders");
        AutomationProperties.SetAutomationId(_files, "browse.files");
        AutomationProperties.SetAutomationId(_refresh, "browse.refresh");
        AutomationProperties.SetAutomationId(_download, "browse.download");
        AutomationProperties.SetAutomationId(_crumbs, "browse.crumbs");

        FluentChrome.TryApplyStyle(_download, "AccentButtonStyle");
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(_refresh);
        toolbar.Children.Add(_download);
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.Opacity = 0.75;
        toolbar.Children.Add(_status);

        _folders.ItemTemplate = ListTemplates.NamedItem("Name");
        _files.ItemTemplate = ListTemplates.FileRow();
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        _empty.TextAlignment = TextAlignment.Center;
        _empty.MaxWidth = 320;

        var filesHost = new Grid();
        filesHost.Children.Add(_files);
        filesHost.Children.Add(_empty);

        var foldersCard = FluentChrome.Section("Folders", "Peer share tree.", _folders);
        var filesCard = FluentChrome.Section("Files", "Double-click a file to download, or a folder to open.", filesHost);

        var split = new Grid { ColumnSpacing = 12 };
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(foldersCard, 0);
        Grid.SetColumn(filesCard, 1);
        split.Children.Add(foldersCard);
        split.Children.Add(filesCard);

        var crumbsCard = FluentChrome.Card(new Border { Child = _crumbs, Padding = new Thickness(12, 8, 12, 8) });
        var toolbarCard = FluentChrome.Card(new Border { Child = toolbar, Padding = new Thickness(12, 10, 12, 10) });

        var root = new Grid { Margin = new Thickness(16), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = FluentChrome.PageHeader("Browse", "Walk a peer’s shares and queue downloads.");
        Grid.SetRow(header, 0);
        Grid.SetRow(crumbsCard, 1);
        Grid.SetRow(toolbarCard, 2);
        Grid.SetRow(split, 3);
        root.Children.Add(header);
        root.Children.Add(crumbsCard);
        root.Children.Add(toolbarCard);
        root.Children.Add(split);
        Content = root;

        _refresh.Click += (_, _) => CommandHelpers.TryExecute(_vm?.Refresh);
        _download.Click += (_, _) => CommandHelpers.TryExecute(_vm?.Download);
        _folders.ItemClick += Folders_ItemClick;
        _crumbs.ItemClicked += Crumbs_ItemClicked;
        _files.ItemClick += Files_ItemClick;
        _files.DoubleTapped += Files_DoubleTapped;
        _files.SelectionChanged += Files_SelectionChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void Files_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionSync || _vm == null) return;
        // Assign without notifying through a full Bind() — LastSelectedEntity changes must not rebuild the list.
        _vm.LastSelectedEntity = _files.SelectedItems.OfType<FileRowModel>().Select(r => r.File).ToList();
    }

    private void Files_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_vm == null || e.ClickedItem is not FileRowModel row) return;
        if (row.File.IsFolder)
            NavigateTo(row.File);
    }

    private void Files_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var row = ResolveFileRow(e.OriginalSource) ?? _files.SelectedItem as FileRowModel;
        if (_vm == null || row == null) return;

        if (row.File.IsFolder)
        {
            NavigateTo(row.File);
            return;
        }

        _vm.LastSelectedEntity = new List<BrowsingFile> { row.File };
        CommandHelpers.TryExecute(_vm.Download);
    }

    private void Folders_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_vm == null || e.ClickedItem is not BrowsingFile file) return;
        NavigateTo(file);
    }

    private void NavigateTo(BrowsingFile file)
    {
        if (_vm == null || file == null) return;
        var path = file.FullPath?.Replace('\\', '/').Trim('/') ?? file.Name;
        if (string.IsNullOrWhiteSpace(path)) return;
        _vm.CurrentPath = path;
    }

    private void Crumbs_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (_vm == null) return;
        // Index 0 is synthetic "Root"
        if (args.Index <= 0)
        {
            _vm.CurrentPath = string.Empty;
            return;
        }

        var parts = (_vm.CurrentPath ?? string.Empty)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (args.Index > parts.Length) return;
        _vm.CurrentPath = string.Join('/', parts.Take(args.Index));
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_vm != null)
            _vm.PropertyChanged -= VmOnPropertyChanged;
        _vm = DataContext as BrowserViewModel;
        _boundCurrentItem = null;
        if (_vm != null)
        {
            _vm.PropertyChanged += VmOnPropertyChanged;
            Bind(forceFiles: true);
        }
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Do not rebuild the file list on selection-only changes — that cancels click/double-click.
        if (e.PropertyName is nameof(BrowserViewModel.LastSelectedEntity))
            return;

        var forceFiles = e.PropertyName is nameof(BrowserViewModel.CurrentItem)
            or nameof(BrowserViewModel.Root)
            or nameof(BrowserViewModel.CurrentPath)
            or null or "";
        Bind(forceFiles);
    }

    private void Bind(bool forceFiles)
    {
        if (_vm == null) return;
        var busy = _vm.IsBusy ? "Loading… " : string.Empty;
        var items = _vm.CurrentItem?.Items;
        var count = items?.Count ?? 0;
        _status.Text = string.IsNullOrEmpty(_vm.Status)
            ? $"{busy}{count} item(s)"
            : $"{busy}{_vm.Status} · {count} item(s)";

        if (!ReferenceEquals(_folders.ItemsSource, _vm.Root))
            _folders.ItemsSource = _vm.Root;

        if (forceFiles || !ReferenceEquals(_boundCurrentItem, _vm.CurrentItem))
        {
            _boundCurrentItem = _vm.CurrentItem;
            var rows = items?.Select(FileRowModel.From).ToList() ?? new List<FileRowModel>();
            _suppressSelectionSync = true;
            try
            {
                _files.ItemsSource = rows;
            }
            finally
            {
                _suppressSelectionSync = false;
            }
        }

        _empty.Text = _vm.IsBusy
            ? "Loading share contents…"
            : "No files or folders in this location.";
        _empty.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _empty.IsHitTestVisible = count == 0;
        _crumbs.ItemsSource = (_vm.CurrentPath ?? string.Empty)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Prepend("Root")
            .ToList();
    }

    private static FileRowModel? ResolveFileRow(object? source)
    {
        DependencyObject? current = source as DependencyObject;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is FileRowModel row)
                return row;
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private sealed class FileRowModel
    {
        public required BrowsingFile File { get; init; }
        public required string Name { get; init; }
        public required string Kind { get; init; }
        public required string SizeText { get; init; }
        public required string ModifiedText { get; init; }

        public static FileRowModel From(BrowsingFile f) => new()
        {
            File = f,
            Name = f.Name,
            Kind = f.IsFolder ? "Folder" : "File",
            SizeText = f.IsFolder ? "—" : Utility.FormatBytes(f.Size),
            ModifiedText = f.LastModified == default ? "—" : f.LastModified.ToString("g")
        };
    }
}

public sealed class SearchView : WinUiViewBase, ISearchView
{
    private readonly TextBox _query = new() { PlaceholderText = "Search shares…" };
    private readonly ListView _results = new()
    {
        SelectionMode = ListViewSelectionMode.Extended,
        IsItemClickEnabled = false
    };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.WrapWholeWords };
    private readonly Button _search = new() { Content = "Search" };
    private readonly Button _download = new() { Content = "Download", IsEnabled = false };
    private readonly Button _reset = new() { Content = "Reset" };
    private SearchViewModel? _vm;
    private bool _resultsBound;

    public SearchView()
    {
        AutomationProperties.SetAutomationId(this, "search.view");
        AutomationProperties.SetAutomationId(_query, "search.query");
        AutomationProperties.SetAutomationId(_results, "search.results");
        AutomationProperties.SetAutomationId(_search, "search.run");
        AutomationProperties.SetAutomationId(_download, "search.download");
        AutomationProperties.SetAutomationId(_reset, "search.reset");
        AutomationProperties.SetAutomationId(_status, "search.status");

        FluentChrome.TryApplyStyle(_search, "AccentButtonStyle");
        _query.HorizontalAlignment = HorizontalAlignment.Stretch;
        var queryRow = new Grid { ColumnSpacing = 8 };
        queryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        queryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        queryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        queryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_query, 0);
        Grid.SetColumn(_search, 1);
        Grid.SetColumn(_download, 2);
        Grid.SetColumn(_reset, 3);
        queryRow.Children.Add(_query);
        queryRow.Children.Add(_search);
        queryRow.Children.Add(_download);
        queryRow.Children.Add(_reset);

        _status.Opacity = 0.75;
        _results.ItemTemplate = ListTemplates.SearchResultRow();
        var empty = FluentChrome.EmptyState(
            "\uE721",
            "No results yet",
            "Search across online peers for filenames. Select hits, then Download.");
        empty.Name = "SearchEmpty";
        var resultsHost = new Grid();
        resultsHost.Children.Add(_results);
        resultsHost.Children.Add(empty);
        _results.Tag = empty;

        var root = new Grid { Margin = new Thickness(16), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = FluentChrome.PageHeader("Search", "Find files across the LAN mesh.");
        var queryCard = FluentChrome.Section("Search", null, queryRow);
        var resultsCard = FluentChrome.Section("Results", null, _status, resultsHost);
        Grid.SetRow(header, 0);
        Grid.SetRow(queryCard, 1);
        Grid.SetRow(resultsCard, 2);
        root.Children.Add(header);
        root.Children.Add(queryCard);
        root.Children.Add(resultsCard);
        Content = root;

        void RunSearch()
        {
            if (_vm != null) _vm.SearchString = _query.Text;
            CommandHelpers.TryExecute(_vm?.Search);
        }

        _search.Click += (_, _) => RunSearch();
        _query.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                RunSearch();
                e.Handled = true;
            }
        };
        _download.Click += (_, _) =>
        {
            var selected = new ObservableCollection<object>();
            foreach (var item in _results.SelectedItems)
                selected.Add(item);
            if (selected.Count == 0)
            {
                _status.Text = "Select one or more results first.";
                return;
            }
            CommandHelpers.TryExecute(_vm?.Download, selected);
            UpdateDownloadEnabled();
        };
        _reset.Click += (_, _) => CommandHelpers.TryExecute(_vm?.Reset);
        _results.SelectionChanged += (_, _) => UpdateDownloadEnabled();
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as SearchViewModel;
            _resultsBound = false;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmChanged;
                Bind(forceResults: true);
            }
        };
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SearchViewModel.Results) or null or "")
            Bind(forceResults: true);
        else
            Bind(forceResults: false);
    }

    private void Bind(bool forceResults)
    {
        if (_vm == null) return;
        if (!_query.FocusState.HasFlag(FocusState.Keyboard))
            _query.Text = _vm.SearchString ?? string.Empty;
        if (forceResults || !_resultsBound)
        {
            _results.ItemsSource = _vm.Results;
            _resultsBound = true;
        }
        var status = $"{_vm.UpperStatusMessage} {_vm.LowerStatusMessage}".Trim();
        _status.Text = string.IsNullOrWhiteSpace(status)
            ? "Ready — enter a filename fragment and Search."
            : status;
        _search.IsEnabled = _vm.AllowSearch;
        if (_results.Tag is UIElement empty)
        {
            var count = (_vm.Results as System.Collections.ICollection)?.Count
                ?? (_vm.Results?.Cast<object>().Count() ?? 0);
            empty.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        UpdateDownloadEnabled();
    }

    private void UpdateDownloadEnabled()
    {
        _download.IsEnabled = _results.SelectedItems.Count > 0;
    }
}

public sealed class SharesView : WinUiViewBase, ISharesView
{
    private readonly ListView _list = new();
    private readonly TextBlock _total = new();
    private readonly Button _remove = new() { Content = "Remove", IsEnabled = false };
    private readonly Button _rename = new() { Content = "Rename", IsEnabled = false };
    private readonly Button _refresh = new() { Content = "Refresh" };
    private readonly ObservableCollection<ShareRowModel> _rows = new();
    private SharesViewModel? _vm;
    private INotifyCollectionChanged? _sharesSource;

    public SharesView()
    {
        AutomationProperties.SetAutomationId(this, "shares.view");
        AutomationProperties.SetAutomationId(_list, "shares.list");

        var add = new Button { Content = "Add" };
        FluentChrome.TryApplyStyle(add, "AccentButtonStyle");
        AutomationProperties.SetAutomationId(add, "shares.add");
        AutomationProperties.SetAutomationId(_remove, "shares.remove");
        AutomationProperties.SetAutomationId(_refresh, "shares.refresh");
        AutomationProperties.SetAutomationId(_rename, "shares.rename");
        AutomationProperties.SetAutomationId(_total, "shares.total");
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        bar.Children.Add(add);
        bar.Children.Add(_remove);
        bar.Children.Add(_refresh);
        bar.Children.Add(_rename);
        _total.VerticalAlignment = VerticalAlignment.Center;
        _total.Opacity = 0.75;
        _total.Margin = new Thickness(12, 0, 0, 0);
        bar.Children.Add(_total);

        _list.ItemTemplate = ListTemplates.ShareRow();
        _list.ItemsSource = _rows;
        _list.IsItemClickEnabled = false;

        var emptyPanel = FluentChrome.EmptyState(
            "\uE8CE",
            "No folders shared yet",
            "Add a local folder to expose it for browse and download on the LAN.");
        emptyPanel.Visibility = Visibility.Collapsed;
        _list.Tag = emptyPanel;

        var listHost = new Grid();
        listHost.Children.Add(_list);
        listHost.Children.Add(emptyPanel);

        var root = new Grid { Margin = new Thickness(16), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = FluentChrome.PageHeader("Shares", "Local folders exposed to peers on the LAN.");
        var toolbarCard = FluentChrome.Card(new Border { Child = bar, Padding = new Thickness(12, 10, 12, 10) });
        var listCard = FluentChrome.Section("Shared folders", null, listHost);
        Grid.SetRow(header, 0);
        Grid.SetRow(toolbarCard, 1);
        Grid.SetRow(listCard, 2);
        root.Children.Add(header);
        root.Children.Add(toolbarCard);
        root.Children.Add(listCard);
        Content = root;

        add.Click += (_, _) => CommandHelpers.TryExecute(_vm?.AddCommand);
        _remove.Click += async (_, _) => await ConfirmRemoveAsync();
        _refresh.Click += (_, _) => CommandHelpers.TryExecute(_vm?.RefreshCommand);
        _rename.Click += (_, _) => CommandHelpers.TryExecute(_vm?.RenameCommand);
        _list.SelectionChanged += (_, _) =>
        {
            if (_vm != null)
                _vm.SelectedShare = (_list.SelectedItem as ShareRowModel)?.Share;
            UpdateShareActions();
        };
        _list.DoubleTapped += async (_, _) => await OpenSelectedFolderAsync();
        var openFolder = new MenuFlyoutItem { Text = "Open folder" };
        AutomationProperties.SetAutomationId(openFolder, "shares.openFolder");
        openFolder.Click += async (_, _) => await OpenSelectedFolderAsync();
        _list.ContextFlyout = new MenuFlyout { Items = { openFolder } };
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnVm;
            _vm = DataContext as SharesViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVm;
                AttachShares(_vm.Shares);
                _total.Text = FormatTotal(_vm);
                UpdateShareActions();
            }
        };
    }

    private void UpdateShareActions()
    {
        var hasSelection = _list.SelectedItem is ShareRowModel;
        _remove.IsEnabled = hasSelection;
        _rename.IsEnabled = hasSelection;
    }

    private async Task ConfirmRemoveAsync()
    {
        if (_list.SelectedItem is not ShareRowModel row) return;
        var dialog = new ContentDialog
        {
            Title = "Remove share?",
            Content = $"Stop sharing “{row.Title}”? Peers will no longer see this folder.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            CommandHelpers.TryExecute(_vm?.RemoveCommand);
    }

    private async Task OpenSelectedFolderAsync()
    {
        if (_list.SelectedItem is not ShareRowModel row) return;
        var path = row.Share.Path;
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
            return;
        await Windows.System.Launcher.LaunchFolderPathAsync(path);
    }

    private void OnVm(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm == null) return;
        if (e.PropertyName is nameof(SharesViewModel.Shares) or null or "")
            AttachShares(_vm.Shares);
        if (e.PropertyName is nameof(SharesViewModel.TotalShareSizeString) or nameof(SharesViewModel.Shares) or null or "")
        {
            // Scan finished: refresh row snapshots (Size/FileCount) as well as the total line.
            RefreshRowSnapshots();
            _total.Text = FormatTotal(_vm);
        }
    }

    private void RefreshRowSnapshots()
    {
        for (var i = 0; i < _rows.Count; i++)
            _rows[i] = ShareRowModel.From(_rows[i].Share);
    }

    private void AttachShares(IEnumerable<Share>? source)
    {
        if (_sharesSource != null)
        {
            _sharesSource.CollectionChanged -= Shares_CollectionChanged;
            _sharesSource = null;
        }

        RebuildRows(source);
        if (source is INotifyCollectionChanged ncc)
        {
            _sharesSource = ncc;
            _sharesSource.CollectionChanged += Shares_CollectionChanged;
        }
    }

    private void Shares_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        void Apply() => RebuildRows(_vm?.Shares);
        if (DispatcherQueue.HasThreadAccess)
            Apply();
        else
            DispatcherQueue.TryEnqueue(Apply);
    }

    private void DetachShareHandlers()
    {
        foreach (var row in _rows)
            row.Share.PropertyChanged -= Share_PropertyChanged;
    }

    private void Share_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Share share) return;
        if (e.PropertyName is not (nameof(Share.Size) or nameof(Share.FileCount) or nameof(Share.Name)
            or nameof(Share.Path) or nameof(Share.Status) or null or ""))
            return;

        void Apply()
        {
            var index = _rows.ToList().FindIndex(r => ReferenceEquals(r.Share, share));
            if (index >= 0)
                _rows[index] = ShareRowModel.From(share);
            if (_vm != null)
                _total.Text = FormatTotal(_vm);
        }

        if (DispatcherQueue.HasThreadAccess)
            Apply();
        else
            DispatcherQueue.TryEnqueue(Apply);
    }

    private void RebuildRows(IEnumerable<Share>? source)
    {
        var selectedId = (_list.SelectedItem as ShareRowModel)?.Share?.ID;
        DetachShareHandlers();
        _rows.Clear();
        if (source != null)
        {
            foreach (var share in source)
            {
                share.PropertyChanged += Share_PropertyChanged;
                _rows.Add(ShareRowModel.From(share));
            }
        }

        if (selectedId != null)
            _list.SelectedItem = _rows.FirstOrDefault(r => r.Share.ID == selectedId);

        if (_list.Tag is UIElement empty)
            empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_vm != null)
            _total.Text = FormatTotal(_vm);
    }

    private static string FormatTotal(SharesViewModel vm)
    {
        long files = 0;
        if (vm.Shares != null)
        {
            lock (vm.Shares)
            {
                files = vm.Shares.Sum(s => s.FileCount);
            }
        }
        return $"Total: {vm.TotalShareSizeString ?? "0 B"} · {files:N0} files";
    }

    private sealed class ShareRowModel
    {
        public required Share Share { get; init; }
        public required string Title { get; init; }
        public required string PathText { get; init; }
        public required string MetaText { get; init; }
        public required string StatusText { get; init; }
        public required string AutomationName { get; init; }

        public static ShareRowModel From(Share s)
        {
            var name = string.IsNullOrWhiteSpace(s.Name) ? "(unnamed share)" : s.Name;
            var status = string.IsNullOrWhiteSpace(s.Status) ? string.Empty : s.Status!;
            var path = s.Path ?? string.Empty;
            return new ShareRowModel
            {
                Share = s,
                Title = name,
                PathText = path,
                MetaText = $"{Utility.FormatBytes(s.Size)} · {s.FileCount:N0} files",
                StatusText = status,
                AutomationName = string.IsNullOrEmpty(status) ? $"{name}, {path}" : $"{name}, {status}, {path}"
            };
        }

        public override string ToString() => AutomationName;
    }
}

public sealed class SettingsView : WinUiViewBase, ISettingsView
{
    private readonly PersonPicture _avatar = new() { Width = 72, Height = 72 };
    private readonly Button _changeAvatar = new() { Content = "Change photo", VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _nickname = new() { Header = "Nickname", MaxLength = 15 };
    private readonly TextBox _description = new() { Header = "Description", MaxLength = 15 };
    private readonly TextBox _downloadDir = new() { Header = "Download folder", IsReadOnly = true };
    private readonly TextBlock _incompleteHint = new()
    {
        FontSize = 12,
        Opacity = 0.75,
        TextWrapping = TextWrapping.WrapWholeWords
    };
    private readonly ComboBox _interface = new()
    {
        Header = "Network interface",
        IsEditable = false,
        DisplayMemberPath = nameof(NetInterface.DisplayLabel)
    };
    private readonly Button _refreshInterfaces = new() { Content = "Refresh", VerticalAlignment = VerticalAlignment.Bottom };
    private readonly NumberBox _maxDownloads = new() { Header = "Max downloads (total)", Minimum = 0, Maximum = 9999, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
    private readonly NumberBox _maxDownloadsPerUser = new() { Header = "Max downloads (per user)", Minimum = 0, Maximum = 9999, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
    private readonly NumberBox _maxUploads = new() { Header = "Max uploads (total)", Minimum = 0, Maximum = 9999, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
    private readonly ComboBox _overlordPri = new() { Header = "LAN coordinator priority" };
    private readonly ToggleSwitch _startup = new() { Header = "Run at startup" };
    private readonly ToggleSwitch _disableCompare = new() { Header = "Disable hardware compare" };
    private readonly ToggleSwitch _noCacheBrowse = new() { Header = "Never cache browsing" };
    private readonly InfoBar _saveBanner = new()
    {
        IsOpen = false,
        IsClosable = true,
        Severity = InfoBarSeverity.Success,
        Title = "Settings saved"
    };
    private SettingsViewModel? _vm;
    private Model? _boundModel;
    private bool _binding;
    private string _snapshot = string.Empty;

    public SettingsView()
    {
        AutomationProperties.SetAutomationId(this, "settings.view");
        AutomationProperties.SetAutomationId(_nickname, "settings.nickname");
        AutomationProperties.SetAutomationId(_description, "settings.description");
        AutomationProperties.SetAutomationId(_downloadDir, "settings.downloadDir");
        AutomationProperties.SetAutomationId(_incompleteHint, "settings.incompleteHint");
        AutomationProperties.SetAutomationId(_avatar, "settings.avatar");
        AutomationProperties.SetAutomationId(_changeAvatar, "settings.changeAvatar");
        AutomationProperties.SetAutomationId(_interface, "settings.interface");
        AutomationProperties.SetAutomationId(_refreshInterfaces, "settings.refreshInterfaces");
        AutomationProperties.SetAutomationId(_overlordPri, "settings.coordinatorPriority");
        AutomationProperties.SetAutomationId(_startup, "settings.startup");
        AutomationProperties.SetAutomationId(_disableCompare, "settings.disableCompare");
        AutomationProperties.SetAutomationId(_noCacheBrowse, "settings.noCacheBrowse");
        AutomationProperties.SetAutomationId(_maxDownloads, "settings.maxDownloads");
        AutomationProperties.SetAutomationId(_maxDownloadsPerUser, "settings.maxDownloadsPerUser");
        AutomationProperties.SetAutomationId(_maxUploads, "settings.maxUploads");
        foreach (var label in new[] { "High", "Normal", "Low" })
            _overlordPri.Items.Add(label);

        var save = new Button { Content = "Save", MinWidth = 96 };
        FluentChrome.TryApplyStyle(save, "AccentButtonStyle");
        AutomationProperties.SetAutomationId(save, "settings.save");
        var cancel = new Button { Content = "Cancel", MinWidth = 96 };
        AutomationProperties.SetAutomationId(cancel, "settings.cancel");
        var browse = new Button { Content = "Browse…", VerticalAlignment = VerticalAlignment.Bottom };
        AutomationProperties.SetAutomationId(browse, "settings.browseDownload");
        var gettingStarted = new Button { Content = "Getting started", VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(gettingStarted, "settings.gettingStarted");
        _refreshInterfaces.VerticalAlignment = VerticalAlignment.Bottom;
        _changeAvatar.VerticalAlignment = VerticalAlignment.Center;

        var avatarRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        avatarRow.Children.Add(_avatar);
        var avatarMeta = new StackPanel
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        avatarMeta.Children.Add(_changeAvatar);
        avatarMeta.Children.Add(new TextBlock
        {
            Text = "Shown to peers on the LAN.",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        avatarRow.Children.Add(avatarMeta);

        var downloadRow = new Grid { ColumnSpacing = 8 };
        downloadRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        downloadRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_downloadDir, 0);
        Grid.SetColumn(browse, 1);
        downloadRow.Children.Add(_downloadDir);
        downloadRow.Children.Add(browse);
        var foldersStack = new StackPanel { Spacing = 8 };
        foldersStack.Children.Add(downloadRow);
        foldersStack.Children.Add(_incompleteHint);

        var interfaceRow = new Grid { ColumnSpacing = 8 };
        interfaceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        interfaceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_interface, 0);
        Grid.SetColumn(_refreshInterfaces, 1);
        interfaceRow.Children.Add(_interface);
        interfaceRow.Children.Add(_refreshInterfaces);

        var headerRow = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 4) };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var pageHeader = new StackPanel { Spacing = 4 };
        pageHeader.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        pageHeader.Children.Add(new TextBlock
        {
            Text = "Identity, folders, network, and transfer limits for this node.",
            FontSize = 13,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        Grid.SetColumn(pageHeader, 0);
        Grid.SetColumn(gettingStarted, 1);
        headerRow.Children.Add(pageHeader);
        headerRow.Children.Add(gettingStarted);

        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16, 16, 16, 16), MaxWidth = 720 };
        panel.Children.Add(headerRow);
        panel.Children.Add(_saveBanner);
        panel.Children.Add(FluentChrome.Section(
            "User",
            "How you appear to other peers.",
            avatarRow,
            _nickname,
            _description));
        panel.Children.Add(FluentChrome.Section(
            "Folders",
            "Where completed downloads are stored.",
            foldersStack));
        panel.Children.Add(FluentChrome.Section(
            "Network",
            "Which interface this node uses on the LAN.",
            interfaceRow,
            _overlordPri));
        panel.Children.Add(FluentChrome.Section(
            "Transfer limits",
            "Cap concurrent downloads and uploads.",
            _maxDownloads,
            _maxDownloadsPerUser,
            _maxUploads));
        panel.Children.Add(FluentChrome.Section(
            "Options",
            "Startup and browse/compare behavior.",
            _startup,
            _disableCompare,
            _noCacheBrowse));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        var actionBar = new Border
        {
            Padding = new Thickness(16, 12, 16, 16),
            Child = new Border
            {
                Child = actions,
                MaxWidth = 720,
                HorizontalAlignment = HorizontalAlignment.Stretch
            }
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroll = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 0);
        Grid.SetRow(actionBar, 1);
        root.Children.Add(scroll);
        root.Children.Add(actionBar);
        Content = root;

        save.Click += async (_, _) =>
        {
            Push();
            CommandHelpers.TryExecute(_vm?.SaveCommand);
            _snapshot = CaptureSnapshot();
            _saveBanner.IsOpen = true;
            await Task.Delay(900);
            App.Services?.GetService<IShellNavigation>()?.GoBack();
        };
        cancel.Click += async (_, _) =>
        {
            if (IsDirty())
            {
                var dialog = new ContentDialog
                {
                    Title = "Discard changes?",
                    Content = "You have unsaved settings. Leave without saving?",
                    PrimaryButtonText = "Discard",
                    CloseButtonText = "Keep editing",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    return;
            }
            CommandHelpers.TryExecute(_vm?.CancelCommand);
            App.Services?.GetService<IShellNavigation>()?.GoBack();
        };
        browse.Click += (_, _) =>
        {
            CommandHelpers.TryExecute(_vm?.EditDownloadDir);
            Bind();
        };
        gettingStarted.Click += (_, _) => CommandHelpers.TryExecute(_vm?.DisplayQuickStart);
        _changeAvatar.Click += (_, _) =>
        {
            CommandHelpers.TryExecute(_vm?.ChangeAvatar);
            Bind();
        };
        _refreshInterfaces.Click += (_, _) =>
        {
            CommandHelpers.TryExecute(_vm?.ResetInterface);
            Bind();
        };
        _interface.SelectionChanged += (_, _) =>
        {
            if (_binding || _vm == null) return;
            if (_interface.SelectedItem is NetInterface nic)
                _vm.SelectedNetworkInterface = nic;
        };
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnVm;
            DetachModel();
            _vm = DataContext as SettingsViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVm;
                AttachModel(_vm.Model);
                Bind();
            }
        };
    }

    private void OnVm(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.Model) or null or "")
            AttachModel(_vm?.Model);
        Bind();
    }

    private void OnModel(object? sender, PropertyChangedEventArgs e) => Bind();

    private void AttachModel(Model? model)
    {
        if (ReferenceEquals(_boundModel, model)) return;
        DetachModel();
        _boundModel = model;
        if (_boundModel != null)
            _boundModel.PropertyChanged += OnModel;
    }

    private void DetachModel()
    {
        if (_boundModel != null)
            _boundModel.PropertyChanged -= OnModel;
        _boundModel = null;
    }

    private void Bind()
    {
        if (_vm?.Model == null || _binding) return;
        _binding = true;
        try
        {
            var m = _vm.Model;
            _nickname.Text = m.Nickname ?? string.Empty;
            _description.Text = m.Description ?? string.Empty;
            _downloadDir.Text = m.DownloadFolder ?? string.Empty;
            var incomplete = string.IsNullOrWhiteSpace(m.IncompleteFolder)
                ? "(derived from download folder)"
                : m.IncompleteFolder;
            _incompleteHint.Text = $"Incomplete downloads go to {incomplete} (derived from the download folder).";
            _maxDownloads.Value = m.MaxDownloads;
            _maxDownloadsPerUser.Value = m.MaxDownloadsPerUser;
            _maxUploads.Value = m.MaxUploads;
            _startup.IsOn = _vm.RunOnStartUp;
            _disableCompare.IsOn = m.DisableComparision;
            _noCacheBrowse.IsOn = m.AlwaysNoCacheBrowsing;
            _overlordPri.SelectedIndex = m.OverlordPriority switch
            {
                OverlordPriority.High => 0,
                OverlordPriority.Low => 2,
                _ => 1
            };

            _interface.ItemsSource = _vm.AvailableInterfaces;
            _interface.SelectedItem = _vm.SelectedNetworkInterface;

            ApplyAvatar(m.Avatar, m.Nickname);
            _snapshot = CaptureSnapshot();
        }
        finally
        {
            _binding = false;
        }
    }

    private string CaptureSnapshot() =>
        string.Join('\u001f',
            _nickname.Text,
            _description.Text,
            _downloadDir.Text,
            _maxDownloads.Value,
            _maxDownloadsPerUser.Value,
            _maxUploads.Value,
            _startup.IsOn,
            _disableCompare.IsOn,
            _noCacheBrowse.IsOn,
            _overlordPri.SelectedIndex,
            (_interface.SelectedItem as NetInterface)?.DisplayLabel);

    private bool IsDirty() => !string.Equals(_snapshot, CaptureSnapshot(), StringComparison.Ordinal);

    private void ApplyAvatar(string? base64, string? nickname)
    {
        _avatar.DisplayName = string.IsNullOrWhiteSpace(nickname) ? "User" : nickname;
        if (string.IsNullOrWhiteSpace(base64))
        {
            _avatar.ProfilePicture = null;
            return;
        }
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var bitmap = new BitmapImage();
            using var stream = new InMemoryRandomAccessStream();
            stream.WriteAsync(bytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            stream.Seek(0);
            bitmap.SetSource(stream);
            _avatar.ProfilePicture = bitmap;
        }
        catch
        {
            _avatar.ProfilePicture = null;
        }
    }

    private void Push()
    {
        if (_vm?.Model == null) return;
        var m = _vm.Model;
        m.Nickname = _nickname.Text;
        m.Description = _description.Text;
        if (!string.IsNullOrWhiteSpace(_downloadDir.Text))
            m.DownloadFolder = _downloadDir.Text;
        if (_interface.SelectedItem is NetInterface nic && nic.Address != null)
        {
            _vm.SelectedNetworkInterface = nic;
            m.LocalNode.Host = nic.Address.ToString();
        }
        m.MaxDownloads = (int)_maxDownloads.Value;
        m.MaxDownloadsPerUser = (int)_maxDownloadsPerUser.Value;
        m.MaxUploads = (int)_maxUploads.Value;
        m.DisableComparision = _disableCompare.IsOn;
        m.AlwaysNoCacheBrowsing = _noCacheBrowse.IsOn;
        m.OverlordPriority = _overlordPri.SelectedIndex switch
        {
            0 => OverlordPriority.High,
            2 => OverlordPriority.Low,
            _ => OverlordPriority.Normal
        };
        _vm.RunOnStartUp = _startup.IsOn;
    }
}

public sealed class DownloadQueueView : WinUiViewBase, IDownloadQueue
{
    private readonly ListView _queue = new();
    private readonly TextBlock _downloadStats = new() { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _uploadStats = new() { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _remove = new() { Content = "Remove", IsEnabled = false };
    private readonly Button _removeAll = new() { Content = "Remove all", IsEnabled = false };
    private readonly Button _move = new() { Content = "Move", IsEnabled = false };
    private DownloadQueueViewModel? _vm;

    public DownloadQueueView()
    {
        AutomationProperties.SetAutomationId(this, "queue.view");
        AutomationProperties.SetAutomationId(_queue, "queue.list");
        AutomationProperties.SetAutomationId(_remove, "queue.remove");
        AutomationProperties.SetAutomationId(_removeAll, "queue.removeAll");
        AutomationProperties.SetAutomationId(_move, "queue.move");
        AutomationProperties.SetAutomationId(_downloadStats, "queue.stats.download");
        AutomationProperties.SetAutomationId(_uploadStats, "queue.stats.upload");

        var moveMenu = new MenuFlyout();
        void AddMove(string label, string id, Func<System.Windows.Input.ICommand?> getCommand)
        {
            var item = new MenuFlyoutItem { Text = label };
            AutomationProperties.SetAutomationId(item, id);
            item.Click += (_, _) => CommandHelpers.TryExecute(getCommand());
            moveMenu.Items.Add(item);
        }
        AddMove("Top", "queue.moveTop", () => _vm?.Movetotop);
        AddMove("Up", "queue.moveUp", () => _vm?.Moveup);
        AddMove("Down", "queue.moveDown", () => _vm?.Movedown);
        AddMove("Bottom", "queue.moveBottom", () => _vm?.Movetobottom);
        _move.Flyout = moveMenu;

        _remove.Click += (_, _) => CommandHelpers.TryExecute(_vm?.RemoveSelection);
        _removeAll.Click += (_, _) => CommandHelpers.TryExecute(_vm?.RemoveAll);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(_remove);
        actions.Children.Add(_removeAll);
        actions.Children.Add(_move);

        _downloadStats.Opacity = 0.75;
        _uploadStats.Opacity = 0.75;
        _downloadStats.FontSize = 12;
        _uploadStats.FontSize = 12;
        ToolTipService.SetToolTip(_downloadStats, "Download transfer totals");
        ToolTipService.SetToolTip(_uploadStats, "Upload transfer totals");
        var statsCol = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, MinWidth = 160 };
        statsCol.Children.Add(_downloadStats);
        statsCol.Children.Add(_uploadStats);

        var bar = new Grid { ColumnSpacing = 12 };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(actions, 0);
        Grid.SetColumn(statsCol, 1);
        bar.Children.Add(actions);
        bar.Children.Add(statsCol);

        _queue.SelectionMode = ListViewSelectionMode.Extended;
        _queue.ItemTemplate = ListTemplates.QueueDownloadRow();
        var empty = FluentChrome.EmptyState(
            "\uE896",
            "Queue is empty",
            "Downloads you start from Browse or Search land here.");
        empty.Visibility = Visibility.Collapsed;
        _queue.Tag = empty;
        var listHost = new Grid();
        listHost.Children.Add(_queue);
        listHost.Children.Add(empty);

        var root = new Grid { Margin = new Thickness(16), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = FluentChrome.PageHeader("Queue", "Reorder and clear queued downloads.");
        var toolbarCard = FluentChrome.Card(new Border { Child = bar, Padding = new Thickness(12, 10, 12, 10) });
        var listCard = FluentChrome.Section("Downloads", null, listHost);
        Grid.SetRow(header, 0);
        Grid.SetRow(toolbarCard, 1);
        Grid.SetRow(listCard, 2);
        root.Children.Add(header);
        root.Children.Add(toolbarCard);
        root.Children.Add(listCard);
        Content = root;

        _queue.SelectionChanged += (_, _) =>
        {
            if (_vm != null)
                _vm.SelectedItems = _queue.SelectedItems.Cast<object>().ToList();
            UpdateQueueActions();
        };
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnVm;
            _vm = DataContext as DownloadQueueViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVm;
                _queue.ItemsSource = _vm.DownloadQueue;
                BindStats();
                UpdateQueueEmpty();
                UpdateQueueActions();
            }
        };
    }

    private void OnVm(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm == null) return;
        if (e.PropertyName is nameof(DownloadQueueViewModel.DownloadQueue) or null or "")
        {
            _queue.ItemsSource = _vm.DownloadQueue;
            UpdateQueueEmpty();
            UpdateQueueActions();
        }
        if (e.PropertyName is nameof(DownloadQueueViewModel.DownloadStats)
            or nameof(DownloadQueueViewModel.UploadStats) or null or "")
            BindStats();
    }

    private void BindStats()
    {
        if (_vm == null) return;
        var dl = string.IsNullOrWhiteSpace(_vm.DownloadStats) ? "Downloads — none yet" : $"Downloads: {_vm.DownloadStats}";
        var ul = string.IsNullOrWhiteSpace(_vm.UploadStats) ? "Uploads — none yet" : $"Uploads: {_vm.UploadStats}";
        _downloadStats.Text = dl;
        _uploadStats.Text = ul;
        ToolTipService.SetToolTip(_downloadStats, _vm.DownloadStats);
        ToolTipService.SetToolTip(_uploadStats, _vm.UploadStats);
    }

    private void UpdateQueueEmpty()
    {
        if (_queue.Tag is not UIElement empty) return;
        var count = QueueCount();
        empty.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateQueueActions()
    {
        var hasSelection = _queue.SelectedItems.Count > 0;
        var hasItems = QueueCount() > 0;
        _remove.IsEnabled = hasSelection;
        _move.IsEnabled = hasSelection;
        _removeAll.IsEnabled = hasItems;
    }

    private int QueueCount() =>
        (_queue.ItemsSource as System.Collections.ICollection)?.Count
        ?? (_queue.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().Count() ?? 0;
}

public sealed class CompareView : WinUiViewBase, ICompareView
{
    private readonly ListView _list = new();
    private readonly TextBlock _status = new();
    private readonly ProgressRing _progress = new()
    {
        Width = 20,
        Height = 20,
        IsActive = false,
        Visibility = Visibility.Collapsed
    };
    private readonly Button _run = new() { Content = "Run compare" };
    private readonly Button _reset = new() { Content = "Reset", IsEnabled = false };
    private readonly ObservableCollection<CompareRowModel> _rows = new();
    private CompareViewModel? _vm;
    private INotifyCollectionChanged? _dataSource;

    public CompareView()
    {
        AutomationProperties.SetAutomationId(this, "compare.view");
        AutomationProperties.SetAutomationId(_list, "compare.list");
        AutomationProperties.SetAutomationId(_run, "compare.run");
        AutomationProperties.SetAutomationId(_reset, "compare.reset");
        AutomationProperties.SetAutomationId(_status, "compare.status");
        FluentChrome.TryApplyStyle(_run, "AccentButtonStyle");
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        bar.Children.Add(_run);
        bar.Children.Add(_reset);
        _progress.VerticalAlignment = VerticalAlignment.Center;
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.Opacity = 0.75;
        bar.Children.Add(_progress);
        bar.Children.Add(_status);

        _list.ItemTemplate = ListTemplates.CompareRow();
        _list.ItemsSource = _rows;

        var emptyPanel = FluentChrome.EmptyState(
            "\uE9D2",
            "No compare results yet",
            "Run compare to collect hardware and share scores from online peers.");
        _list.Tag = emptyPanel;
        var listHost = new Grid();
        listHost.Children.Add(_list);
        listHost.Children.Add(emptyPanel);

        var root = new Grid { Margin = new Thickness(16), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = FluentChrome.PageHeader("Compare", "Rank peers by hardware score, latency, and share size.");
        var toolbarCard = FluentChrome.Card(new Border { Child = bar, Padding = new Thickness(12, 10, 12, 10) });
        var listCard = FluentChrome.Section("Results", null, listHost);
        Grid.SetRow(header, 0);
        Grid.SetRow(toolbarCard, 1);
        Grid.SetRow(listCard, 2);
        root.Children.Add(header);
        root.Children.Add(toolbarCard);
        root.Children.Add(listCard);
        Content = root;

        _run.Click += (_, _) => CommandHelpers.TryExecute(_vm?.Run);
        _reset.Click += async (_, _) => await ConfirmResetAsync();
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnVm;
            DetachData();
            _vm = DataContext as CompareViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVm;
                AttachData(_vm.Data);
                BindCompareChrome();
            }
        };
    }

    private void OnVm(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm == null) return;
        if (e.PropertyName is nameof(CompareViewModel.Data) or null or "")
            AttachData(_vm.Data);
        if (e.PropertyName is nameof(CompareViewModel.Status)
            or nameof(CompareViewModel.EnableRun) or null or "")
            BindCompareChrome();
    }

    private void BindCompareChrome()
    {
        if (_vm == null) return;
        var raw = _vm.Status?.Trim() ?? string.Empty;
        var status = raw.Length == 0 || raw.Equals("Idle", StringComparison.OrdinalIgnoreCase)
            ? "Ready — run compare when peers are online."
            : raw;
        _status.Text = status;
        _run.IsEnabled = _vm.EnableRun;
        var collecting = !_vm.EnableRun && raw.Contains("Collect", StringComparison.OrdinalIgnoreCase);
        _progress.IsActive = collecting;
        _progress.Visibility = collecting ? Visibility.Visible : Visibility.Collapsed;
        UpdateResetEnabled();
    }

    private void UpdateResetEnabled()
    {
        var hasRows = _rows.Count > 0;
        var busy = _progress.IsActive;
        _reset.IsEnabled = hasRows && !busy;
    }

    private async Task ConfirmResetAsync()
    {
        if (_rows.Count == 0) return;
        var dialog = new ContentDialog
        {
            Title = "Clear compare results?",
            Content = "Remove the current peer scores from this list.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            CommandHelpers.TryExecute(_vm?.Reset);
    }

    private void AttachData(IEnumerable<CompareNode>? source)
    {
        DetachData();
        RebuildRows(source);
        if (source is INotifyCollectionChanged ncc)
        {
            _dataSource = ncc;
            _dataSource.CollectionChanged += Data_CollectionChanged;
        }
    }

    private void DetachData()
    {
        if (_dataSource != null)
        {
            _dataSource.CollectionChanged -= Data_CollectionChanged;
            _dataSource = null;
        }
    }

    private void Data_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        void Apply() => RebuildRows(_vm?.Data);
        if (DispatcherQueue.HasThreadAccess)
            Apply();
        else
            DispatcherQueue.TryEnqueue(Apply);
    }

    private void RebuildRows(IEnumerable<CompareNode>? source)
    {
        _rows.Clear();
        if (source != null)
        {
            foreach (var node in source.OrderByDescending(n => n.Score).ThenBy(n => n.LatencyMs))
                _rows.Add(CompareRowModel.From(node));
        }

        if (_list.Tag is UIElement empty)
            empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateResetEnabled();
    }

    private sealed class CompareRowModel
    {
        public required string Title { get; init; }
        public required string ScoreText { get; init; }
        public required string StatusText { get; init; }
        public required string HardwareText { get; init; }
        public required string MetaText { get; init; }
        public required string AutomationName { get; init; }

        public static CompareRowModel From(CompareNode n)
        {
            var nick = string.IsNullOrWhiteSpace(n.Nickname) ? "(unnamed)" : n.Nickname;
            var cpu = string.IsNullOrWhiteSpace(n.CPUType) ? "CPU unknown" : n.CPUType;
            if (n.CPUCores > 0)
                cpu = $"{cpu} · {n.CPUCores}c/{Math.Max(n.CPUThreads, n.CPUCores)}t";
            var ram = n.RAMSize > 0 ? Utility.FormatBytes(n.RAMSize) : "RAM —";
            var status = string.IsNullOrWhiteSpace(n.Status) ? string.Empty : n.Status!;
            return new CompareRowModel
            {
                Title = nick,
                ScoreText = n.Score > 0 ? n.Score.ToString("N0") : "—",
                StatusText = status,
                HardwareText = $"{cpu} · {ram} RAM",
                MetaText = $"{n.Location} · {n.LatencyMs} ms · {Utility.FormatBytes(n.ShareSize)} shares",
                AutomationName = $"{nick}, score {n.Score}, {status}"
            };
        }

        public override string ToString() => AutomationName;
    }
}

public sealed class ConversationView : WinUiViewBase, IConverstationView
{
    private readonly ListView _messages = new();
    private readonly TextBox _input = new() { PlaceholderText = "Message" };
    private ConversationViewModel? _vm;

    public ConversationView()
    {
        var send = new Button { Content = "Send" };
        FluentChrome.TryApplyStyle(send, "AccentButtonStyle");
        var close = new Button { Content = "Close" };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _input.HorizontalAlignment = HorizontalAlignment.Stretch;
        var inputRow = new Grid { ColumnSpacing = 8 };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_input, 0);
        Grid.SetColumn(send, 1);
        Grid.SetColumn(close, 2);
        inputRow.Children.Add(_input);
        inputRow.Children.Add(send);
        inputRow.Children.Add(close);

        var root = new Grid { Margin = new Thickness(16), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = FluentChrome.PageHeader("Conversation", "One-to-one chat with a peer.");
        var messagesCard = FluentChrome.Section("Messages", null, _messages);
        var inputCard = FluentChrome.Card(new Border { Child = inputRow, Padding = new Thickness(12, 10, 12, 10) });
        Grid.SetRow(header, 0);
        Grid.SetRow(messagesCard, 1);
        Grid.SetRow(inputCard, 2);
        root.Children.Add(header);
        root.Children.Add(messagesCard);
        root.Children.Add(inputCard);
        Content = root;

        send.Click += (_, _) =>
        {
            if (_vm != null) _vm.CurrentChatMessage = _input.Text;
            CommandHelpers.TryExecute(_vm?.SendChatMessage);
            _input.Text = string.Empty;
        };
        close.Click += (_, _) => CommandHelpers.TryExecute(_vm?.Close);
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnVm;
            _vm = DataContext as ConversationViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVm;
                Bind();
            }
        };
    }

    private void OnVm(object? sender, PropertyChangedEventArgs e) => Bind();

    private void Bind()
    {
        if (_vm?.Conversation == null) return;
        _messages.ItemsSource = _vm.Conversation.UIMessages;
        _input.Text = _vm.CurrentChatMessage ?? string.Empty;
    }
}

public sealed class WebHelpView : WinUiViewBase, IWebPanel
{
    private readonly WebView2 _web = new();
    private string _location = string.Empty;

    public string Location
    {
        get => _location;
        set
        {
            _location = value ?? string.Empty;
            _ = NavigateAsync();
        }
    }

    public WebHelpView()
    {
        Content = _web;
        Loaded += async (_, _) => await NavigateAsync();
    }

    private async Task NavigateAsync()
    {
        if (string.IsNullOrWhiteSpace(_location)) return;
        try
        {
            await _web.EnsureCoreWebView2Async();
            var path = System.IO.Path.GetFullPath(_location);
            if (System.IO.File.Exists(path))
                _web.Source = new Uri(path);
            else if (Uri.TryCreate(_location, UriKind.Absolute, out var uri))
                _web.Source = uri;
        }
        catch
        {
            Content = new TextBlock { Text = $"Help: {_location}", Margin = new Thickness(16) };
        }
    }
}

public sealed class MessageBoxView : WinUiViewBase, IMessageBoxView
{
    public bool? ShowDialog() => true;
    public bool? ShowDialog(object parent) => true;
}

public sealed class InterfaceSelectionView : WinUiViewBase, IInterfaceSelectionView
{
    public bool? ShowDialog() => true;
    public void Close() { }
}

public sealed class QueryService : IQuery
{
    private readonly Window _window;

    public QueryService(Window window) => _window = window;

    public bool SelectFolder(out string result)
    {
        result = string.Empty;
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
            var folder = picker.PickSingleFolderAsync().GetAwaiter().GetResult();
            if (folder == null) return false;
            result = folder.Path;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SelectFile(out string result)
    {
        result = string.Empty;
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
            var file = picker.PickSingleFileAsync().GetAwaiter().GetResult();
            if (file == null) return false;
            result = file.Path;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SelectImageFile(out string result)
    {
        result = string.Empty;
        try
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" })
                picker.FileTypeFilter.Add(ext);
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
            var file = picker.PickSingleFileAsync().GetAwaiter().GetResult();
            if (file == null) return false;
            result = file.Path;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
