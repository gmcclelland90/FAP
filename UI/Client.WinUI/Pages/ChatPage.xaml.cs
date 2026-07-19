using System.Linq;
using FAP.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Fap.Client.WinUI.Pages;

public sealed partial class ChatPage : Page
{
    private IChatSession? _session;
    private bool _binding;
    private bool _subscribed;

    public ChatPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += (_, _) => EnsureBound();
    }

    public void EnsureBound()
    {
        if (App.Services is null)
            return;
        if (_session != null)
        {
            RefreshThreads();
            RefreshMessages();
            return;
        }

        _session = App.Services.GetRequiredService<IChatSession>();
        _session.Load();
        if (!_subscribed)
        {
            _session.SelectedChanged += (_, _) => DispatcherQueue.TryEnqueue(() =>
            {
                RefreshMessages();
                RefreshThreads();
            });
            _session.ThreadsChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshThreads);
            _session.UnreadChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshThreads);
            _subscribed = true;
        }
        RefreshThreads();
        RefreshMessages();
    }

    public void OpenPeerId(string? peerId)
    {
        EnsureBound();
        if (_session == null)
            return;
        if (string.IsNullOrEmpty(peerId) || peerId == ChatThread.NetworkId)
            _session.SelectNetwork();
        else
        {
            var thread = _session.Threads.FirstOrDefault(t => t.Id == peerId);
            if (thread != null)
                _session.SelectThread(thread);
        }
        RefreshThreads();
        RefreshMessages();
    }

    private void RefreshThreads()
    {
        if (_session == null)
            return;
        _binding = true;
        ThreadList.Items.Clear();
        ChatThread? selected = _session.SelectedThread;
        foreach (var t in _session.Threads)
        {
            var item = new ListViewItem
            {
                Content = BuildThreadContent(t),
                Tag = t,
                IsSelected = ReferenceEquals(t, selected)
            };
            ThreadList.Items.Add(item);
            if (ReferenceEquals(t, selected))
                ThreadList.SelectedItem = item;
        }
        _binding = false;
        ApplyThreadChrome(selected);
    }

    private static UIElement BuildThreadContent(ChatThread thread)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = thread.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (thread.UnreadCount > 0)
            title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;

        Grid.SetColumn(title, 0);
        grid.Children.Add(title);

        if (thread.UnreadCount > 0)
        {
            var badge = new InfoBadge
            {
                Value = Math.Min(thread.UnreadCount, 99),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current.Resources.TryGetValue("AttentionInfoBadgeStyle", out var style) && style is Style s)
                badge.Style = s;
            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);
        }

        return grid;
    }

    private void RefreshMessages()
    {
        if (_session == null)
            return;
        var thread = _session.SelectedThread;
        ApplyThreadChrome(thread);
        MessageList.Items.Clear();
        if (thread == null)
        {
            MessagesEmpty.Visibility = Visibility.Visible;
            return;
        }

        foreach (var m in thread.Messages)
            MessageList.Items.Add(BuildMessageRow(m));

        var empty = MessageList.Items.Count == 0;
        MessagesEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (!empty)
            MessageList.ScrollIntoView(MessageList.Items[^1]);
        Composer.Text = _session.Draft ?? string.Empty;
    }

    private void ApplyThreadChrome(ChatThread? thread)
    {
        ThreadTitle.Text = thread?.Title ?? "Chat";
        if (thread == null)
        {
            ThreadSubtitle.Text = "Pick a conversation";
            Composer.PlaceholderText = "Message…";
            MessagesEmptyTitle.Text = "No conversation selected";
            MessagesEmptyBody.Text = "Choose LAN for a broadcast, or a peer for a private message.";
            return;
        }

        if (thread.IsNetwork || thread.Id == ChatThread.NetworkId)
        {
            ThreadSubtitle.Text = "Broadcast to connected peers";
            Composer.PlaceholderText = "Message the LAN…";
            MessagesEmptyTitle.Text = "No LAN messages yet";
            MessagesEmptyBody.Text =
                "LAN chat reaches every connected peer. Pick a peer conversation for a private message.";
        }
        else
        {
            var online = thread.Peer?.Online == true;
            ThreadSubtitle.Text = online ? "Peer · online" : "Peer · offline or unknown";
            Composer.PlaceholderText = $"Message {thread.Title}…";
            MessagesEmptyTitle.Text = "No messages yet";
            MessagesEmptyBody.Text =
                online
                    ? $"Private chat with {thread.Title}. Messages stay on the mesh."
                    : $"{thread.Title} may be offline — you can still draft; delivery waits until they are reachable.";
        }
    }

    private static UIElement BuildMessageRow(string line)
    {
        var isYou = line.StartsWith("You:", StringComparison.Ordinal);
        var isSystem = line.StartsWith("System:", StringComparison.Ordinal);

        var body = new TextBlock
        {
            Text = line,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        if (isYou)
            body.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        if (isSystem)
            body.Opacity = 0.75;

        var panel = new StackPanel
        {
            Spacing = 2,
            Padding = new Thickness(8, 6, 8, 6)
        };
        panel.Children.Add(body);

        if (isSystem)
        {
            var meta = new TextBlock
            {
                Text = "System",
                FontSize = 11,
                Opacity = 0.55
            };
            panel.Children.Insert(0, meta);
        }
        else if (isYou)
        {
            var meta = new TextBlock
            {
                Text = "You",
                FontSize = 11,
                Opacity = 0.55
            };
            panel.Children.Insert(0, meta);
            // Strip redundant "You:" prefix from body when meta shows it
            if (line.Length > 4)
                body.Text = line[4..].TrimStart();
        }

        return panel;
    }

    private void ThreadList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding || _session == null)
            return;
        if (ThreadList.SelectedItem is ListViewItem { Tag: ChatThread thread })
        {
            _session.SelectThread(thread);
            RefreshMessages();
            RefreshThreads();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => Send();

    private void Composer_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            Send();
        }
    }

    private void Send()
    {
        if (_session == null)
            return;
        _session.Draft = Composer.Text ?? string.Empty;
        _session.SendCurrentMessage();
        Composer.Text = string.Empty;
        RefreshMessages();
        RefreshThreads();
    }
}
