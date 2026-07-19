using System;
using System.Collections.ObjectModel;
using FAP.Domain.Entities;

namespace FAP.Application.Services;

public interface IChatSession
{
    ObservableCollection<ChatThread> Threads { get; }
    ChatThread? SelectedThread { get; }
    int TotalUnread { get; }
    event EventHandler? SelectedChanged;
    event EventHandler? ThreadsChanged;
    event EventHandler? UnreadChanged;
    void Load();
    void SavePeerHistory();
    void SelectNetwork();
    void OpenPeer(Node peer);
    void SelectThread(ChatThread thread);
    void SendCurrentMessage();
    string Draft { get; set; }
}

public sealed class ChatThread
{
    public const string NetworkId = "__network__";

    public string Id { get; init; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsNetwork { get; init; }
    public Node? Peer { get; set; }
    public ObservableCollection<string> Messages { get; } = new();
    public int UnreadCount { get; set; }
}
