# File System & Sharing System

## Overview

The File System & Sharing System is the core component responsible for managing shared files and folders across the FAP network. It provides a unified interface for browsing, searching, and accessing files from multiple shares across different peers.

## Architecture

### Core Components

```
┌─────────────────────────────────────┐
│         ShareInfoService            │  ← File system coordination
├─────────────────────────────────────┤
│         Share Entity                │  ← Share configuration
├─────────────────────────────────────┤
│    File System Entities             │  ← Data structures
│  ┌─────────────┬─────────────────┐ │
│  │ BrowsingFile│ Directory/File  │ │
│  └─────────────┴─────────────────┘ │
├─────────────────────────────────────┤
│         BrowseVerb                  │  ← Network protocol
└─────────────────────────────────────┘
```

## Share Management

### Share Entity

The `Share` entity represents a configured file share on the local system:

```csharp
public class Share : BaseEntity
{
    public string ID { get; set; }           // Unique identifier
    public string Name { get; set; }         // Display name
    public string Path { get; set; }         // Local file system path
    public long Size { get; set; }           // Total size in bytes
    public long FileCount { get; set; }      // Number of files
    public DateTime LastRefresh { get; set; } // Last scan time
    public string Status { get; set; }       // Current status
}
```

### Share Configuration

Shares are configured through the UI and stored in the application model:

```csharp
// Example share configuration
var share = new Share
{
    Name = "Documents",
    Path = "C:\\Users\\Username\\Documents",
    ID = IDService.CreateID()
};
model.Shares.Add(share);
```

## File System Entities

### BrowsingFile

`BrowsingFile` represents a file or folder in the browsing interface:

```csharp
public class BrowsingFile : BaseEntity
{
    public bool IsFolder { get; set; }           // True if directory
    public string Name { get; set; }             // File/folder name
    public long Size { get; set; }               // Size in bytes
    public DateTime LastModified { get; set; }   // Modification time
    public string Path { get; set; }             // Relative path
    public bool IsPopulated { get; set; }        // UI state
    public ObservableCollection<BrowsingFile> Items { get; set; }
}
```

### Directory Entity

`Directory` represents a folder in the file system hierarchy:

```csharp
public class Directory : File
{
    public long ItemCount { get; set; }              // Total items
    public List<Directory> SubDirectories { get; set; }
    public List<File> Files { get; set; }
    
    public void Save(string id) { /* ... */ }
    public void Load(string id) { /* ... */ }
    public void Clean() { /* ... */ }
}
```

### File Entity

`File` represents a single file in the system:

```csharp
public class File : IComparable
{
    public string Name { get; set; }           // Filename
    public long Size { get; set; }             // File size
    public long LastModified { get; set; }     // Modification time
}
```

## ShareInfoService

The `ShareInfoService` is the central coordinator for file system operations:

### Key Responsibilities

1. **Share Management**: Loading, refreshing, and managing share configurations
2. **Path Resolution**: Converting network paths to local file system paths
3. **Caching**: Maintaining cached file system information
4. **Search**: Providing file search capabilities across all shares

### Core Methods

#### Path Resolution

```csharp
public bool ToLocalPath(string input, out string[] output)
{
    // Converts network path like "/Documents/folder" 
    // to local paths like "C:\\Users\\Username\\Documents\\folder"
}
```

#### Directory Browsing

```csharp
public bool GetPath(string path, bool noCache, bool distinct, out List<BrowsingFile> results)
{
    // Returns list of files/folders for a given path
    // Handles multiple shares with same name
    // Supports caching and distinct results
}
```

#### File System Scanning

```csharp
private void RefreshFileInfo(DirectoryInfo directory, Directory model)
{
    // Scans actual file system and updates cached data
    // Recursively processes subdirectories
    // Calculates sizes and item counts
}
```

## File System Caching

### Cache Location

```csharp
public static readonly string SaveLocation = 
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\FAP\ShareInfo\";
```

### Cache Format

- **Protocol Buffers**: Used for efficient serialization
- **File Naming**: `{base64(shareID)}.cache`
- **Automatic Loading**: Cached data loaded on startup
- **Background Refresh**: Shares refreshed periodically

### Cache Structure

```csharp
public class RootShare
{
    public string ID { get; set; }           // Share identifier
    public Directory Data { get; set; }      // Cached directory tree
}
```

## Virtual Directory Support

### Multiple Share Handling

When multiple shares have the same name, the system creates virtual directories:

```csharp
public Directory GetPath(string path, out bool isVirtual)
{
    // Returns virtual directory combining multiple shares
    // Merges files and folders from all matching shares
    // Maintains distinct results
}
```

### Virtual Directory Properties

- **Combined Content**: Files and folders from all matching shares
- **Aggregated Statistics**: Total size and item counts
- **Sorted Results**: Alphabetically ordered files and folders
- **Memory Management**: Virtual directories cleaned after use

## File Search System

### Search Implementation

```csharp
public List<SearchResult> Search(string expression, int limit, 
    long modifiedBefore, long modifiedAfter, 
    double smallerThan, double largerThan)
{
    // Searches across all shares
    // Supports wildcard patterns
    // Filters by date and size
    // Limits results for performance
}
```

### Search Features

- **Pattern Matching**: Wildcard support (`*` and `?`)
- **Date Filtering**: Modified before/after dates
- **Size Filtering**: Files larger/smaller than specified size
- **Cross-Share Search**: Searches all configured shares
- **Result Limiting**: Prevents excessive memory usage

### StringMatcher Class

```csharp
public class StringMatcher
{
    public StringMatcher(string expression) { /* ... */ }
    public bool IsMatch(string name) { /* ... */ }
}
```

## Network Integration

### BrowseVerb

The `BrowseVerb` handles file system requests over the network:

```csharp
public class BrowseVerb : BaseVerb, IVerb
{
    public bool NoCache { get; set; }        // Skip cache
    public string Path { get; set; }          // Requested path
    public List<BrowsingFile> Results { get; set; }
    
    public NetworkRequest ProcessRequest(NetworkRequest r)
    {
        // Processes browse request
        // Returns file/folder listing
        // Handles caching directives
    }
}
```

### HTTP Integration

The web interface is served by ASP.NET Core static files and a modern HTTP handler (`ModernHTTPHandler`) that supports range requests, HEAD, and ETag/If-None-Match/If-Range for efficient downloads and caching.

## Performance Optimizations

### Caching Strategy

1. **Memory Cache**: Active shares kept in memory
2. **Disk Cache**: Persistent cache files for large shares
3. **Background Refresh**: Periodic updates without blocking UI
4. **Lazy Loading**: Directory contents loaded on demand

### Memory Management

```csharp
public void Clean()
{
    // Clears virtual directories
    // Prevents memory leaks
    // Called after processing
}
```

### Async Operations

```csharp
ThreadPool.QueueUserWorkItem(DoRefreshPath, share);
// Background share scanning
// Non-blocking UI updates
```

## Error Handling

### File System Errors

- **Access Denied**: Graceful handling of permission issues
- **Network Unavailable**: Fallback to cached data
- **Corrupted Cache**: Automatic cache regeneration
- **Invalid Paths**: Safe path validation

### Recovery Mechanisms

```csharp
try
{
    RefreshFileInfo(directory, model);
}
catch (Exception e)
{
    // Log error and continue
    // Maintain system stability
}
```

## Configuration

### Share Settings

- **Auto-Refresh**: Periodic share scanning
- **Cache Duration**: How long to keep cached data
- **Search Limits**: Maximum search results
- **File Size Limits**: Maximum file sizes to index

### Performance Tuning

- **Scan Intervals**: How often to refresh shares
- **Memory Limits**: Maximum cache memory usage
- **Concurrent Scans**: Number of simultaneous scans
- **Timeout Settings**: Operation timeouts

## Security Considerations

### Path Validation

- **Path Traversal**: Prevention of directory traversal attacks
- **Access Control**: Respect for file system permissions
- **Input Sanitization**: Safe handling of user input
- **Share Isolation**: Separation between different shares

### Data Protection

- **Local Only**: File content never transmitted automatically
- **User Initiated**: Downloads only when explicitly requested
- **No Indexing**: File content not indexed or analyzed
- **Privacy Respect**: Respects file system access controls 