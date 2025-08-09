using System.Text.Json.Serialization;
using FAP.Domain.Entities;
using FAP.Domain.Entities.FileSystem;
using FAP.Domain.Verbs;

namespace FAP.Domain
{
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
        PropertyNameCaseInsensitive = true
    )]
    [JsonSerializable(typeof(Model))]
    [JsonSerializable(typeof(DownloadQueue))]
    [JsonSerializable(typeof(DownloadRequest))]
    [JsonSerializable(typeof(Share))]
    [JsonSerializable(typeof(Node))]
    [JsonSerializable(typeof(Overlord))]
    [JsonSerializable(typeof(SearchResult))]
    [JsonSerializable(typeof(BrowsingFile))]
    [JsonSerializable(typeof(FAP.Shared.Entities.NetworkRequest))]
    // Verbs for protocol payloads
    [JsonSerializable(typeof(BrowseVerb))]
    [JsonSerializable(typeof(CompareVerb))]
    [JsonSerializable(typeof(SearchVerb))]
    [JsonSerializable(typeof(InfoVerb))]
    [JsonSerializable(typeof(ChatVerb))]
    [JsonSerializable(typeof(NoopVerb))]
    [JsonSerializable(typeof(UpdateVerb))]
    [JsonSerializable(typeof(ConversationVerb))]
    public partial class FapJsonContext : JsonSerializerContext
    {
    }
}


