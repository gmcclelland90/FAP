namespace FAP.Shared.Models
{
    public class BrowsePageData
    {
        public string Nickname { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public string AppVersion { get; set; } = string.Empty;
        public string FreeLimit { get; set; } = string.Empty;
        public int MaxUploadSlots { get; set; }
        public int FreeUploadSlots { get; set; }
        public string QueueInfo { get; set; } = string.Empty;
        public string SlotColour { get; set; } = "green";
        public int QueueLength { get; set; }
        public bool PathResolved { get; set; } = true;
        public string CurrentPath { get; set; } = "/";
        public string TotalSize { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public bool IsSearch { get; set; }
        public bool SearchScopeHere { get; set; }
        public int SearchResultCount { get; set; }
        public bool SearchTruncated { get; set; }
        public List<PathSegment> PathSegments { get; set; } = new();
        public List<BrowseFileEntry> Files { get; set; } = new();
    }

    public class PathSegment
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    public class BrowseFileEntry
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>Relative name segment (browse) or encoded file name (legacy).</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>Absolute site href for the name link (preferred).</summary>
        public string Href { get; set; } = string.Empty;
        /// <summary>Parent location shown under the name in search results.</summary>
        public string LocationText { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string IconHtml { get; set; } = string.Empty;
        public bool HasIcon { get; set; }
        public long Size { get; set; }
        public string SizeText { get; set; } = string.Empty;
        public string LastModifiedText { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}
