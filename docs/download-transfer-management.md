# Download & Transfer Management

## Overview

The Download & Transfer Management system handles all file transfer operations in FAP, including downloads, uploads, queue management, and bandwidth control. It provides a robust, resumable transfer system with comprehensive monitoring and logging.

## Architecture

### Core Components

```
┌─────────────────────────────────────┐
│         DownloadQueue               │  ← Queue management
├─────────────────────────────────────┤
│      DownloadWorkerService          │  ← Download processing
├─────────────────────────────────────┤
│      TransferSession                │  ← Active transfer tracking
├─────────────────────────────────────┤
│    ServerUploadLimiterService       │  ← Upload bandwidth control
├─────────────────────────────────────┤
│      TransferLog                    │  ← Transfer history
└─────────────────────────────────────┘
```

## Download Queue System

### DownloadQueue Entity

The `DownloadQueue` manages the list of pending downloads:

```csharp
public class DownloadQueue : BaseEntity
{
    private readonly SafeObservedCollection<DownloadRequest> queue;
    private readonly string saveLocation = "Queue.cfg";
    
    public SafeObservedCollection<DownloadRequest> List { get; }
    
    public void Save() { /* ... */ }
    public void Load() { /* ... */ }
}
```

### DownloadRequest Entity

Each download request contains:

```csharp
public class DownloadRequest : BaseEntity
{
    public string FullPath { get; set; }        // Remote file path
    public string LocalPath { get; set; }       // Local directory
    public string FileName { get; set; }        // Filename
    public string FolderPath { get; set; }      // Directory path
    public long Size { get; set; }              // File size
    public bool IsFolder { get; set; }          // Is directory
    public string ClientID { get; set; }        // Source node ID
    public string Nickname { get; set; }        // Source nickname
    public DateTime Added { get; set; }         // Queue time
    public DownloadRequestState State { get; set; }
    public int NextTryTime { get; set; }        // Retry timing
}
```

### Download States

```csharp
public enum DownloadRequestState
{
    None,           // Initial state
    Queued,         // In download queue
    Downloading,    // Currently downloading
    Downloaded,     // Successfully completed
    Error           // Failed with error
}
```

## Download Worker System

### DownloadWorkerService

The `DownloadWorkerService` processes downloads for a specific remote node:

```csharp
public class DownloadWorkerService : ITransferWorker
{
    private readonly Queue<DownloadRequest> queue;
    private readonly Node remoteNode;
    private readonly NetworkSpeedMeasurement netSpeed;
    private readonly BufferService bufferService;
    
    public bool IsQueueFull { get; }
    public Node Node { get; }
    public event EventHandler OnWorkerFinished;
    
    public void AddDownload(DownloadRequest item) { /* ... */ }
    private void process(object o) { /* ... */ }
}
```

### Queue Management

#### Queue Limits

```csharp
public bool IsQueueFull
{
    get
    {
        lock (sync)
        {
            if (queue.Count > 10)                    // Max 10 items
                return true;
            if (queue.Sum(s => s.Size) > 262144000)  // Max 256MB total
                return true;
            return false;
        }
    }
}
```

#### Worker Lifecycle

1. **Creation**: New worker created for each remote node
2. **Queue Processing**: Sequential processing of download requests
3. **Completion**: Worker marked complete when queue empty
4. **Cleanup**: Worker removed from active list

### Download Processing

#### File Downloads

```csharp
// File download process
if (!currentItem.IsFolder)
{
    // 1. Create HTTP request
    var req = (HttpWebRequest)WebRequest.Create(url);
    
    // 2. Handle resume support
    if (fileStream.Length > 0)
        req.AddRange(fileStream.Length);
    
    // 3. Stream data with progress tracking
    using (Stream responseStream = resp.GetResponseStream())
    {
        while (bytesRead > 0)
        {
            position += bytesRead;
            netSpeed.PutData(bytesRead);
            // Process data...
        }
    }
}
```

#### Folder Downloads

```csharp
// Folder download process
if (currentItem.IsFolder)
{
    // 1. Browse folder contents
    var verb = new BrowseVerb(null);
    verb.Path = currentItem.FullPath;
    verb.NoCache = true;
    
    // 2. Add all items to queue
    foreach (BrowsingFile item in verb.Results)
    {
        newItems.Add(new DownloadRequest
        {
            FullPath = currentItem.FullPath + "/" + item.Name,
            IsFolder = item.IsFolder,
            Size = item.Size,
            // ... other properties
        });
    }
    model.DownloadQueue.List.AddRange(newItems);
}
```

## Transfer Session Management

### TransferSession Entity

Tracks active transfers for UI display:

```csharp
public class TransferSession : BaseEntity
{
    private readonly ITransferWorker worker;
    
    public ITransferWorker Worker { get; }
    public long Size { get; set; }
    public long Speed { get; set; }
    public long Position { get; set; }
    public string Status { get; set; }
    public string User { get; set; }
    public bool IsDownload { get; set; }
    public int Percent { get; set; }
}
```

### ITransferWorker Interface

Common interface for all transfer operations:

```csharp
public interface ITransferWorker
{
    long Length { get; }        // Total size
    bool IsComplete { get; }    // Transfer status
    long Speed { get; }         // Current speed
    string Status { get; }      // Status message
    long Position { get; }      // Current position
}
```

## Upload Management

### Upload Limiters

#### ServerUploadLimiterService

Controls concurrent uploads and bandwidth:

```csharp
public class ServerUploadLimiterService
{
    private readonly List<ServerUploadToken> activeTokenList;
    private readonly Queue<ServerUploadToken> recycledList;
    
    public ServerUploadToken RequestUploadToken(string node) { /* ... */ }
    public void FreeToken(ServerUploadToken token) { /* ... */ }
    public int GetActiveTokenCount() { /* ... */ }
    public int GetQueueLength() { /* ... */ }
}
```

#### ServerUploadToken

Represents an upload slot:

```csharp
public class ServerUploadToken
{
    public string RemoteEndPoint { get; set; }
    public int GlobalQueuePosition { get; set; }
    
    public void WaitTimeout() { /* ... */ }
}
```

### Upload Workers

#### Transfer Tracking

Uploads/downloads are tracked via lightweight session objects (`LightweightTransferWorker`) and server-side range support is implemented in the HTTP handler for downloads. Legacy uploader classes have been removed.

## Transfer Logging

### TransferLog Entity

Records completed transfers:

```csharp
public class TransferLog : BaseEntity
{
    public string Nickname { get; set; }     // Source user
    public string Filename { get; set; }     // File name
    public string Path { get; set; }         // File path
    public long Size { get; set; }           // File size
    public int Speed { get; set; }           // Average speed
    public DateTime Added { get; set; }      // Start time
    public DateTime Completed { get; set; }  // End time
}
```

### Logging Integration

```csharp
// Download completion logging
var rxlog = new TransferLog();
rxlog.Nickname = currentItem.Nickname;
rxlog.Completed = DateTime.Now;
rxlog.Filename = currentItem.FileName;
rxlog.Path = currentItem.FolderPath;
rxlog.Size = currentItem.Size - resumePoint;
if (0 != seconds)
    rxlog.Speed = (int)(rxlog.Size / seconds);
model.CompletedDownloads.Add(rxlog);
```

## Watchdog Controller

### Queue Monitoring

The `WatchdogController` manages the download queue:

```csharp
public class WatchdogController
{
    private readonly List<DownloadWorkerService> workers;
    
    private void ScanForDownloads()
    {
        // 1. Check for new downloads
        foreach (DownloadRequest item in model.DownloadQueue.List)
        {
            // 2. Find appropriate worker
            // 3. Add to worker queue
            // 4. Create new worker if needed
        }
        
        // 5. Clean up completed workers
        foreach (DownloadWorkerService worker in workers.Where(w => w.IsComplete))
        {
            workers.Remove(worker);
        }
    }
}
```

### Worker Management

#### Worker Creation Rules

```csharp
// Create new worker if:
if (workers.Where(w => w.Node == client).Count() < model.MaxDownloadsPerUser &&
    workers.Count < model.MaxDownloads)
{
    var worker = new DownloadWorkerService(client, model, bufferService);
    workers.Add(worker);
    worker.AddDownload(item);
}
```

#### Queue Distribution

```csharp
// Try existing worker first
foreach (DownloadWorkerService worker in workers.Where(w => w.Node == client))
{
    if (!worker.IsQueueFull)
    {
        worker.AddDownload(item);
        break;
    }
}
```

## Performance Optimizations

### Buffer Management

```csharp
public class BufferService
{
    public MemoryBuffer GetBuffer() { /* ... */ }
    public MemoryBuffer GetSmallBuffer() { /* ... */ }
    public void FreeBuffer(MemoryBuffer buffer) { /* ... */ }
    public void Clean() { /* ... */ }
}
```

### Speed Measurement

```csharp
public class NetworkSpeedMeasurement
{
    public void PutData(int bytes) { /* ... */ }
    public long GetSpeed() { /* ... */ }
    public static NetworkSpeedMeasurement TotalDownload { get; }
    public static NetworkSpeedMeasurement TotalUpload { get; }
}
```

### Resume Support

```csharp
// Check for existing file
if (File.Exists(mainPath))
{
    fileStream = File.Open(mainPath, FileMode.Open, FileAccess.Write, FileShare.None);
    incompletePath = mainPath;
}
else
{
    fileStream = File.Open(incompletePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
}
```

## Configuration

### Download Settings

```csharp
public class Model
{
    public int MaxDownloads { get; set; }              // Global limit
    public int MaxDownloadsPerUser { get; set; }       // Per-user limit
    public int MaxUploads { get; set; }                // Upload limit
    public int MaxUploadsPerUser { get; set; }         // Per-user upload limit
    public string DownloadFolder { get; set; }         // Download directory
    public string IncompleteFolder { get; set; }       // Temporary directory
}
```

### Queue Persistence

```csharp
// Save queue to disk
public void Save()
{
    lock (sync)
        SafeSave(this, saveLocation, Formatting.None);
}

// Load queue from disk
public void Load()
{
    lock (sync)
    {
        if (File.Exists(DATA_FOLDER + saveLocation))
        {
            var saved = SafeLoad<DownloadQueue>(saveLocation);
            queue.AddRange(saved.List.ToList());
        }
    }
}
```

## Error Handling

### Retry Mechanism

```csharp
// Failed download handling
currentItem.State = DownloadRequestState.Error;
currentItem.NextTryTime = Environment.TickCount + Model.DOWNLOAD_RETRY_TIME;
```

### Queue Position Updates

```csharp
// Example: bounded upload queue status (if applicable)
```

### Resource Cleanup

```csharp
finally
{
    bufferService.FreeBuffer(buffer);
    if (null != session)
        model.TransferSessions.Remove(session);
    if (null != token)
        uploadLimiter.FreeToken(token);
}
```

## Security Considerations

### File Access Control

- **Read-Only Access**: Files opened with `FileShare.Read`
- **Permission Respect**: Respects file system permissions
- **Path Validation**: Validates all file paths
- **Access Logging**: Logs all file access attempts

### Network Security

- **Connection Limits**: Prevents excessive connections
- **Bandwidth Control**: Prevents network abuse
- **Queue Limits**: Prevents memory exhaustion
- **Timeout Handling**: Prevents hanging connections

## Monitoring and Statistics

### Transfer Statistics

```csharp
// Calculate transfer statistics
long totalSize = list.Sum(s => s.Size);
long speed = list.Sum(s => (long)s.Speed) / list.Count;
string stats = string.Format("{0} transferred in {1} files at average of {2}",
    Utility.FormatBytes(totalSize), list.Count,
    Utility.ConvertNumberToTextSpeed(speed));
```

### Real-time Monitoring

- **Active Transfers**: Current transfer sessions
- **Queue Status**: Pending download count
- **Speed Monitoring**: Real-time transfer speeds
- **Progress Tracking**: Individual file progress 