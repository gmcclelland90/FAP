using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Services;

/// <summary>Persists per-peer DM history only (not Network/global chat).</summary>
public sealed class ChatHistoryStore
{
    private const int MaxMessagesPerPeer = 500;
    private readonly ILogger<ChatHistoryStore> _logger;
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ChatHistoryStore(ILogger<ChatHistoryStore> logger)
    {
        _logger = logger;
        var folder = Environment.GetEnvironmentVariable("FAP_DATA_FOLDER");
        if (string.IsNullOrWhiteSpace(folder))
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FAP");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "ChatHistory.json");
    }

    public ChatHistoryFile Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new ChatHistoryFile();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ChatHistoryFile>(json, JsonOptions) ?? new ChatHistoryFile();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load chat history from {Path}", _path);
            return new ChatHistoryFile();
        }
    }

    public void Save(ChatHistoryFile file)
    {
        try
        {
            foreach (var c in file.Conversations)
            {
                if (c.Messages.Count > MaxMessagesPerPeer)
                    c.Messages = c.Messages.Skip(c.Messages.Count - MaxMessagesPerPeer).ToList();
            }

            var json = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save chat history to {Path}", _path);
        }
    }
}

public sealed class ChatHistoryFile
{
    public List<PersistedPeerChat> Conversations { get; set; } = new();
}

public sealed class PersistedPeerChat
{
    public string PeerId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public List<PersistedChatMessage> Messages { get; set; } = new();
}

public sealed class PersistedChatMessage
{
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset Utc { get; set; }
}
