using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FAP.Application.Services;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class ConversationController : IConversationController, IChatSession
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ConversationController> _logger;
        private readonly ILogger<ModernHttpClient> _httpLogger;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
        private readonly Model _model;
        private readonly ChatHistoryStore _store;
        private readonly object _gate = new();
        private readonly Dictionary<string, ChatThread> _peers = new(StringComparer.Ordinal);
        private ChatThread? _selected;
        private string _draft = string.Empty;
        private bool _loaded;
        private CancellationTokenSource? _saveCts;

        public ConversationController(
            IServiceProvider serviceProvider,
            Model model,
            ChatHistoryStore store)
        {
            _services = serviceProvider;
            _model = model;
            _store = store;
            _logger = serviceProvider.GetRequiredService<ILogger<ConversationController>>();
            _httpClientFactory = serviceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            _httpLogger = serviceProvider.GetRequiredService<ILogger<ModernHttpClient>>();

            NetworkThread = new ChatThread
            {
                Id = ChatThread.NetworkId,
                Title = "LAN",
                IsNetwork = true
            };
            Threads = new ObservableCollection<ChatThread> { NetworkThread };
        }

        public ChatThread NetworkThread { get; }
        public ObservableCollection<ChatThread> Threads { get; }
        public ChatThread? SelectedThread
        {
            get => _selected;
            private set
            {
                _selected = value;
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? SelectedChanged;
        public event EventHandler? ThreadsChanged;
        public event EventHandler? UnreadChanged;

        public int TotalUnread
        {
            get
            {
                var total = NetworkThread.UnreadCount;
                foreach (var t in _peers.Values)
                    total += t.UnreadCount;
                return total;
            }
        }

        public string Draft
        {
            get => _draft;
            set => _draft = value ?? string.Empty;
        }

        public void Load()
        {
            lock (_gate)
            {
                if (_loaded)
                    return;
                _loaded = true;

                SyncNetworkFromModel();
                _model.Messages.CollectionChanged += ModelMessages_CollectionChanged;

                var file = _store.Load();
                foreach (var persisted in file.Conversations)
                {
                    if (string.IsNullOrWhiteSpace(persisted.PeerId))
                        continue;
                    var thread = GetOrCreatePeerThread(persisted.PeerId, persisted.Nickname, peer: null);
                    thread.Messages.Clear();
                    foreach (var m in persisted.Messages)
                        thread.Messages.Add(m.Text);
                }

                SelectedThread ??= NetworkThread;
            }
        }

        public void SavePeerHistory()
        {
            lock (_gate)
            {
                var file = new ChatHistoryFile();
                foreach (var t in _peers.Values)
                {
                    file.Conversations.Add(new PersistedPeerChat
                    {
                        PeerId = t.Id,
                        Nickname = t.Title,
                        Messages = t.Messages.Select(text => new PersistedChatMessage
                        {
                            Text = text,
                            Utc = DateTimeOffset.UtcNow
                        }).ToList()
                    });
                }
                _store.Save(file);
            }
        }

        public void SelectNetwork()
        {
            Load();
            MarkRead(NetworkThread);
            SelectedThread = NetworkThread;
        }

        public void OpenPeer(Node peer)
        {
            if (peer == null || string.IsNullOrEmpty(peer.ID))
                return;
            Load();
            var thread = GetOrCreatePeerThread(peer.ID, peer.Nickname, peer);
            thread.Peer = peer;
            thread.Title = string.IsNullOrWhiteSpace(peer.Nickname) ? peer.ID : peer.Nickname;
            MarkRead(thread);
            SelectedThread = thread;
            ScheduleSave();
        }

        public void SelectThread(ChatThread thread)
        {
            if (thread == null)
                return;
            Load();
            MarkRead(thread);
            SelectedThread = thread;
        }

        public void SendCurrentMessage()
        {
            Load();
            var text = Draft?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return;

            var thread = SelectedThread ?? NetworkThread;
            if (thread.IsNetwork)
            {
                if (text == "/disconnect")
                {
                    _model.Messages.Add("Disconnecting from current overlord..");
                    _services.GetRequiredService<ConnectionController>().Disconnect();
                }
                else
                {
                    _model.Messages.AddRotate(_model.Nickname + ":" + text, 50);
                    SafeObservingCollectionManager.UpdateNowAsync();
                    _services.GetRequiredService<ConnectionController>().SendMessage(text);
                }
                Draft = string.Empty;
                return;
            }

            _ = SendPeerMessageAsync(thread, text);
        }

        public bool HandleMessage(string id, string nickname, string message)
        {
            try
            {
                Load();
                var thread = GetOrCreatePeerThread(id, nickname, peer: null);
                if (!string.IsNullOrWhiteSpace(nickname))
                    thread.Title = nickname;
                var line = $"{nickname}: {message}";
                thread.Messages.Add(line);
                Trim(thread);
                if (!ReferenceEquals(SelectedThread, thread))
                {
                    thread.UnreadCount++;
                    RaiseUnreadChanged();
                }
                ScheduleSave();
                ThreadsChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling conversation message");
                return false;
            }
        }

        private async Task SendPeerMessageAsync(ChatThread thread, string text)
        {
            try
            {
                Draft = string.Empty;
                thread.Messages.Add($"You: {text}");
                Trim(thread);
                ScheduleSave();

                var peer = thread.Peer ?? _model.Network.Nodes.FirstOrDefault(n => n.ID == thread.Id);
                if (peer == null)
                {
                    thread.Messages.Add("System: Peer is offline; message kept locally.");
                    return;
                }

                thread.Peer = peer;
                var verb = new ConversationVerb
                {
                    Message = text,
                    Nickname = _model.Nickname
                };
                var client = new ModernHttpClient(_model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));
                var ok = await client.ExecuteAsync(verb, peer);
                if (ok)
                    FAP.Shared.FapMetrics.Inc(ref FAP.Shared.FapMetrics.ConversationSent);
                else
                    thread.Messages.Add("System: Failed to deliver message.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending peer chat message");
                thread.Messages.Add("System: Error sending message.");
            }
        }

        private ChatThread GetOrCreatePeerThread(string peerId, string nickname, Node? peer)
        {
            if (_peers.TryGetValue(peerId, out var existing))
            {
                if (peer != null)
                    existing.Peer = peer;
                return existing;
            }

            var thread = new ChatThread
            {
                Id = peerId,
                Title = string.IsNullOrWhiteSpace(nickname) ? peerId : nickname,
                IsNetwork = false,
                Peer = peer
            };
            _peers[peerId] = thread;
            // Keep Network first
            Threads.Add(thread);
            ThreadsChanged?.Invoke(this, EventArgs.Empty);
            return thread;
        }

        private void SyncNetworkFromModel()
        {
            NetworkThread.Messages.Clear();
            foreach (var m in _model.Messages)
                NetworkThread.Messages.Add(m);
        }

        private void ModelMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                SyncNetworkFromModel();
                if (SelectedThread?.IsNetwork == true)
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                var viewingLan = ReferenceEquals(SelectedThread, NetworkThread);
                foreach (var item in e.NewItems)
                {
                    if (item is not string line)
                        continue;
                    NetworkThread.Messages.Add(line);
                    if (!viewingLan && IsInboundNetworkLine(line))
                        NetworkThread.UnreadCount++;
                }
                Trim(NetworkThread);
                if (!viewingLan && NetworkThread.UnreadCount > 0)
                    RaiseUnreadChanged();
                if (viewingLan)
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                ThreadsChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            SyncNetworkFromModel();
            if (SelectedThread?.IsNetwork == true)
                SelectedChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool IsInboundNetworkLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;
            if (line.StartsWith("You:", StringComparison.Ordinal))
                return false;
            var nick = _model.Nickname ?? string.Empty;
            if (!string.IsNullOrEmpty(nick) && line.StartsWith(nick + ":", StringComparison.Ordinal))
                return false;
            return true;
        }

        private void MarkRead(ChatThread thread)
        {
            if (thread.UnreadCount == 0)
                return;
            thread.UnreadCount = 0;
            RaiseUnreadChanged();
        }

        private void RaiseUnreadChanged() => UnreadChanged?.Invoke(this, EventArgs.Empty);

        private static void Trim(ChatThread thread)
        {
            while (thread.Messages.Count > 500)
                thread.Messages.RemoveAt(0);
        }

        private void ScheduleSave()
        {
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(750, token);
                    SavePeerHistory();
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }
    }
}
