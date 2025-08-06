# Configuration & Settings

## Overview

The Configuration & Settings system manages all application configuration, user preferences, and persistent data. It provides a centralized approach to storing and retrieving application state, with support for backward compatibility and automatic default value assignment.

## Architecture

### Configuration Structure

```
┌─────────────────────────────────────┐
│              Model                  │  ← Main Configuration Entity
├─────────────────────────────────────┤
│         SettingsController          │  ← Settings Management
├─────────────────────────────────────┤
│         SettingsViewModel           │  ← UI Binding
├─────────────────────────────────────┤
│         BaseEntity                  │  ← Persistence Base
└─────────────────────────────────────┘
```

### Configuration Hierarchy

```
Model (Main Configuration)
├── LocalNode (Node Configuration)
├── Shares (File Share Configuration)
├── Network (Network Configuration)
├── DownloadQueue (Download Configuration)
└── Transfer Settings
    ├── MaxDownloads
    ├── MaxUploads
    ├── DownloadFolder
    └── IncompleteFolder
```

## Model Entity

### Core Configuration Properties

The `Model` class serves as the central configuration entity:

```csharp
[Serializable]
public class Model : BaseEntity, IDataErrorInfo
{
    // Application Information
    public static readonly string AppVersion = "FAP Beat 7.5ish";
    public static readonly string ProtocolVersion = "FAP/1.0";
    
    // Network Configuration
    public static int UPLINK_TIMEOUT = 60000;        // 1 minute
    public static int DOWNLOAD_RETRY_TIME = 120000;  // 2 minutes
    public static int FREE_FILE_LIMIT = 1048576;     // 1MB
    public static int MAX_SEARCH_RESULTS = 10000;
    
    // User Configuration
    public string Nickname { get; set; }
    public string Description { get; set; }
    public string Avatar { get; set; }
    
    // Transfer Configuration
    public int MaxDownloads { get; set; }
    public int MaxDownloadsPerUser { get; set; }
    public int MaxUploads { get; set; }
    public int MaxUploadsPerUser { get; set; }
    
    // Folder Configuration
    public string DownloadFolder { get; set; }
    public string IncompleteFolder { get; set; }
    
    // Feature Configuration
    public bool DisableComparision { get; set; }
    public bool AlwaysNoCacheBrowsing { get; set; }
    public bool DisplayedHelp { get; set; }
    
    // Network Configuration
    public OverlordPriority OverlordPriority { get; set; }
    public Network Network { get; set; }
    public Node LocalNode { get; set; }
    
    // Collections
    public SafeObservedCollection<Share> Shares { get; set; }
    public DownloadQueue DownloadQueue { get; set; }
}
```

### Configuration Validation

The Model implements `IDataErrorInfo` for validation:

```csharp
public string this[string columnName]
{
    get
    {
        if (null == columnName || columnName == "Nickname")
        {
            if (string.IsNullOrEmpty(Nickname))
                return "Please enter a nickname";
        }
        if (null == columnName || columnName == "MaxDownloads")
        {
            if (MaxDownloads < 0)
                return "Please enter a positive number";
        }
        if (null == columnName || columnName == "MaxUploads")
        {
            if (MaxUploads < 1)
                return "You must allow at least one upload!";
        }
        return null;
    }
}
```

## Default Configuration

### Port Configuration
- **Client Port**: Defaults to port 30 for client applications
- **Overlord Port**: Uses port 40 when starting overlord servers
- **Port Assignment**: 
  - Clients use port 30 for web interface and client operations
  - Overlords use port 40 for server operations and client connections

### Automatic Default Assignment

```csharp
public void CheckSetDefaults()
{
    // Node ID and Secret
    if (string.IsNullOrEmpty(LocalNode.ID))
        LocalNode.ID = IDService.CreateID();
    
    if (string.IsNullOrEmpty(LocalNode.Secret))
        LocalNode.Secret = IDService.CreateID();
    
    // Default to port 30 for client applications
    if (LocalNode.Port == 0)
        LocalNode.Port = 30;
    
    // Default Avatar
    if (string.IsNullOrEmpty(Avatar))
    {
        Stream stream = Application.GetResourceStream(
            new Uri("Images/Default_Avatar.png", UriKind.Relative)).Stream;
        var img = new byte[stream.Length];
        stream.Read(img, 0, (int)stream.Length);
        Avatar = Convert.ToBase64String(img);
        Save();
    }
    
    // Default Nickname
    if (string.IsNullOrEmpty(Nickname))
    {
        string user = WindowsIdentity.GetCurrent().Name;
        if (!string.IsNullOrEmpty(user) && user.Contains('\\'))
        {
            user = user.Substring(user.IndexOf('\\') + 1);
        }
        
        // Use PC name for default users
        if (string.IsNullOrEmpty(user) ||
            string.Equals(user, "Administrator", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(user, "Guest", StringComparison.InvariantCultureIgnoreCase))
            user = Dns.GetHostName();
        
        Nickname = user;
    }
    
    // Default Transfer Limits
    if (MaxDownloads == 0)
        MaxDownloads = 3;
    if (MaxDownloadsPerUser == 0)
        MaxDownloadsPerUser = 3;
    if (MaxUploads == 0)
        MaxUploads = 3;
    if (MaxUploadsPerUser == 0)
        MaxUploadsPerUser = 4;
    
    // Default Download Folder
    if (string.IsNullOrEmpty(DownloadFolder))
        DownloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) +
                         "\\FAP Downloads";
    
    if (!Directory.Exists(DownloadFolder))
        Directory.CreateDirectory(DownloadFolder);
}
```

## Configuration Persistence

### BaseEntity Persistence

All configuration entities inherit from `BaseEntity`:

```csharp
[DataContract]
public class BaseEntity : INotifyPropertyChanged
{
    protected readonly string DATA_FOLDER = 
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\FAP\";
    
    private static readonly string BACKUP_EXT = ".bak";
    
    protected void SafeSave(object o, string fileName, Formatting f)
    {
        if (!Directory.Exists(DATA_FOLDER))
            Directory.CreateDirectory(DATA_FOLDER);
        
        string obj = JsonConvert.SerializeObject(o, f);
        File.WriteAllText(DATA_FOLDER + fileName, obj);
        File.WriteAllText(DATA_FOLDER + fileName + BACKUP_EXT, obj);
    }
    
    protected T SafeLoad<T>(string fileName)
    {
        try
        {
            if (File.Exists(DATA_FOLDER + fileName))
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(DATA_FOLDER + fileName));
        }
        catch { }
        
        try
        {
            if (File.Exists(DATA_FOLDER + fileName + BACKUP_EXT))
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(DATA_FOLDER + fileName + BACKUP_EXT));
        }
        catch { }
        
        throw new Exception("Unable to read " + fileName);
    }
}
```

### Model Persistence

```csharp
public void Save()
{
    lock (downloadQueue)
    {
        SafeSave(this, saveLocation, Formatting.Indented);
    }
}

public void Load()
{
    lock (downloadQueue)
    {
        try
        {
            if (File.Exists(DATA_FOLDER + saveLocation))
            {
                var saved = SafeLoad<Model>(saveLocation);
                
                // Restore configuration
                Shares.Clear();
                Shares.AddRange(saved.Shares.OrderBy(s => s.Name).ToList());
                Avatar = saved.Avatar;
                Description = saved.Description;
                Nickname = saved.Nickname;
                DownloadFolder = saved.DownloadFolder;
                MaxDownloads = saved.MaxDownloads;
                MaxDownloadsPerUser = saved.MaxDownloadsPerUser;
                MaxUploads = saved.MaxUploads;
                MaxUploadsPerUser = saved.MaxUploadsPerUser;
                DisableComparision = saved.DisableComparision;
                LocalNode = saved.LocalNode;
                AlwaysNoCacheBrowsing = saved.AlwaysNoCacheBrowsing;
                OverlordPriority = saved.OverlordPriority;
                DisplayedHelp = saved.DisplayedHelp;
            }
            else if (File.Exists(Legacy.Model.saveLocation))
            {
                // Legacy configuration migration
                MigrateLegacyConfiguration();
            }
        }
        catch (Exception e)
        {
            LogManager.GetLogger("faplog").Warn("Failed to read config", e);
        }
    }
}
```

### Legacy Configuration Migration

```csharp
private void MigrateLegacyConfiguration()
{
    var oldmodel = new Legacy.Model();
    oldmodel.Load();
    
    // Migrate settings from legacy format
    Shares.Clear();
    Shares.AddRange(oldmodel.Shares.OrderBy(s => s.Name).ToList());
    Avatar = oldmodel.Avatar;
    Description = oldmodel.Description;
    Nickname = oldmodel.Nickname;
    DownloadFolder = oldmodel.DownloadFolder;
    MaxDownloads = oldmodel.MaxDownloads;
    MaxDownloadsPerUser = oldmodel.MaxDownloadsPerUser;
    MaxUploads = oldmodel.MaxUploads;
    MaxUploadsPerUser = oldmodel.MaxUploadsPerUser;
    DisableComparision = oldmodel.DisableComparision;
    
    // Create new LocalNode from legacy data
    LocalNode = new Node();
    foreach (var data in oldmodel.Node.Data)
        LocalNode.SetData(data.Key, data.Value);
    
    AlwaysNoCacheBrowsing = oldmodel.AlwaysNoCacheBrowsing;
    OverlordPriority = OverlordPriority.Normal;
    
    // Save migrated configuration
    Save();
}
```

## Settings Management

### SettingsController

Manages settings UI and operations:

```csharp
internal class SettingsController
{
    private readonly IContainer container;
    private readonly ApplicationCore core;
    private readonly Model model;
    private SettingsViewModel viewModel;
    
    public void Initaize()
    {
        if (null == viewModel)
        {
            viewModel = container.Resolve<SettingsViewModel>();
            viewModel.Model = model;
            viewModel.EditDownloadDir = new DelegateCommand(SettingsEditDownloadDir);
            viewModel.ChangeAvatar = new DelegateCommand(ChangeAvatar);
            viewModel.ResetInterface = new DelegateCommand(ResetInterface);
            viewModel.DisplayQuickStart = new DelegateCommand(DisplayQuickStart);
        }
    }
    
    private void SettingsEditDownloadDir()
    {
        string folder = string.Empty;
        if (browser.SelectFolder(out folder))
        {
            model.DownloadFolder = folder;
            model.IncompleteFolder = folder + "\\Incomplete";
        }
    }
    
    private void ChangeAvatar()
    {
        string path = string.Empty;
        if (browser.SelectFile(out path))
        {
            try
            {
                // Load and resize avatar image
                var ms = new MemoryStream();
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                ms.SetLength(stream.Length);
                stream.Read(ms.GetBuffer(), 0, (int)stream.Length);
                ms.Flush();
                stream.Close();
                
                // Resize to 100x100
                var bitmap = new Bitmap(ms);
                Image thumbnail = ResizeImage(bitmap, 100, 100);
                ms = new MemoryStream();
                thumbnail.Save(ms, ImageFormat.Png);
                model.Avatar = Convert.ToBase64String(ms.ToArray());
            }
            catch { }
        }
    }
    
    private void ResetInterface()
    {
        model.LocalNode.Host = null;
        model.Save();
        container.Resolve<IMessageService>().ShowWarning(
            "Interface selection reset. FAP will now restart.");
        
        // Restart application
        var notePad = new Process();
        notePad.StartInfo.FileName = Assembly.GetEntryAssembly().CodeBase;
        notePad.StartInfo.Arguments = "WAIT";
        notePad.Start();
        core.Exit();
    }
}
```

### SettingsViewModel

UI binding for settings:

```csharp
public class SettingsViewModel : ViewModel<ISettingsView>, IDataErrorInfo
{
    private Model model;
    private ICommand editDownloadDir;
    private ICommand changeAvatar;
    private ICommand resetInterface;
    private ICommand displayQuickStart;
    
    public Model Model
    {
        get { return model; }
        set
        {
            model = value;
            RaisePropertyChanged("Model");
        }
    }
    
    public ICommand EditDownloadDir
    {
        get { return editDownloadDir; }
        set
        {
            editDownloadDir = value;
            RaisePropertyChanged("EditDownloadDir");
        }
    }
    
    public bool RunOnStartUp
    {
        set
        {
            if (value)
                RegistryHelper.SetRegistryData(Registry.CurrentUser, startupRegistryPath, 
                    "FAP", GetStartupCommand());
            else
                RegistryHelper.SetRegistryData(Registry.CurrentUser, startupRegistryPath, 
                    "FAP", string.Empty);
            RaisePropertyChanged("RunOnStartUp");
        }
        get
        {
            return (RegistryHelper.GetRegistryData(Registry.CurrentUser, 
                startupRegistryPath + "/FAP") == GetStartupCommand());
        }
    }
    
    private string GetStartupCommand()
    {
        string location = Assembly.GetEntryAssembly().Location;
        return string.Format("\"{0}\" STARTUP", location);
    }
}
```

## Configuration Categories

### Network Configuration

```csharp
// Network settings
public Network Network { get; set; }
public Node LocalNode { get; set; }
public OverlordPriority OverlordPriority { get; set; }

// Network constants
public static int UPLINK_TIMEOUT = 60000;        // 1 minute
public static int DOWNLOAD_RETRY_TIME = 120000;  // 2 minutes
```

### Transfer Configuration

```csharp
// Download settings
public int MaxDownloads { get; set; }              // Global download limit
public int MaxDownloadsPerUser { get; set; }       // Per-user download limit
public string DownloadFolder { get; set; }         // Download directory
public string IncompleteFolder { get; set; }       // Temporary directory

// Upload settings
public int MaxUploads { get; set; }                // Global upload limit
public int MaxUploadsPerUser { get; set; }         // Per-user upload limit

// Transfer constants
public static int FREE_FILE_LIMIT = 1048576;       // 1MB free file limit
```

### User Configuration

```csharp
// User identity
public string Nickname { get; set; }               // Display name
public string Description { get; set; }            // User description
public string Avatar { get; set; }                 // Base64 encoded avatar

// User preferences
public bool DisableComparision { get; set; }       // Disable system comparison
public bool AlwaysNoCacheBrowsing { get; set; }    // Always refresh file lists
public bool DisplayedHelp { get; set; }            // Help display status
```

### Share Configuration

```csharp
// File shares
public SafeObservedCollection<Share> Shares { get; set; }

// Share properties
public class Share : BaseEntity
{
    public string ID { get; set; }                 // Unique identifier
    public string Name { get; set; }               // Display name
    public string Path { get; set; }               // Local file system path
    public long Size { get; set; }                 // Total size
    public long FileCount { get; set; }            // Number of files
    public DateTime LastRefresh { get; set; }      // Last scan time
    public string Status { get; set; }             // Current status
}
```

## Configuration Validation

### Property Validation

```csharp
public string this[string columnName]
{
    get
    {
        // Nickname validation
        if (null == columnName || columnName == "Nickname")
        {
            if (string.IsNullOrEmpty(Nickname))
                return "Please enter a nickname";
        }
        
        // Download limits validation
        if (null == columnName || columnName == "MaxDownloads")
        {
            if (MaxDownloads < 0)
                return "Please enter a positive number";
        }
        if (null == columnName || columnName == "MaxDownloadsPerUser")
        {
            if (MaxDownloadsPerUser < 0)
                return "Please enter a positive number";
        }
        
        // Upload limits validation
        if (null == columnName || columnName == "MaxUploads")
        {
            if (MaxUploads < 1)
                return "You must allow at least one upload!";
        }
        
        return null;
    }
}
```

### Configuration Integrity

```csharp
public void ValidateConfiguration()
{
    // Ensure required directories exist
    if (!Directory.Exists(DownloadFolder))
        Directory.CreateDirectory(DownloadFolder);
    
    if (!Directory.Exists(IncompleteFolder))
        Directory.CreateDirectory(IncompleteFolder);
    
    // Validate share paths
    foreach (var share in Shares)
    {
        if (!Directory.Exists(share.Path))
        {
            share.Status = "Path not found";
        }
    }
    
    // Validate transfer limits
    if (MaxDownloads < 0) MaxDownloads = 3;
    if (MaxDownloadsPerUser < 0) MaxDownloadsPerUser = 3;
    if (MaxUploads < 1) MaxUploads = 3;
    if (MaxUploadsPerUser < 1) MaxUploadsPerUser = 4;
}
```

## Configuration Security

### Shutdown Protection

```csharp
private readonly ReaderWriterLockSlim shutdownLock;

public void GetAntiShutdownLock()
{
    shutdownLock.EnterReadLock();
}

public void ReleaseAntiShutdownLock()
{
    shutdownLock.ExitReadLock();
}

public void GetShutdownLock()
{
    shutdownLock.TryEnterWriteLock(4000);
}
```

### Backup Strategy

```csharp
protected void SafeSave(object o, string fileName, Formatting f)
{
    string obj = JsonConvert.SerializeObject(o, f);
    
    // Save primary file
    File.WriteAllText(DATA_FOLDER + fileName, obj);
    
    // Save backup file
    File.WriteAllText(DATA_FOLDER + fileName + BACKUP_EXT, obj);
    
    obj = null;
}
```

## Configuration Monitoring

### Property Change Notification

```csharp
protected void NotifyChange(string path)
{
    if (null != PropertyChanged)
        PropertyChanged(this, new PropertyChangedEventArgs(path));
}

public event PropertyChangedEventHandler PropertyChanged;
```

### Configuration Change Handling

```csharp
private void LocalNode_PropertyChanged(object sender, PropertyChangedEventArgs e)
{
    // Update immediately on user input for better UX
    if (e.PropertyName == "Nickname" || 
        e.PropertyName == "Description" || 
        e.PropertyName == "Avatar")
    {
        ThreadPool.QueueUserWorkItem(updateModelAsync);
    }
}

private void updateModelAsync(object o)
{
    connectionController.CheckModelChanges();
}
```

## Configuration Extensions

### Registry Integration

```csharp
public bool RunOnStartUp
{
    set
    {
        if (value)
            RegistryHelper.SetRegistryData(Registry.CurrentUser, startupRegistryPath, 
                "FAP", GetStartupCommand());
        else
            RegistryHelper.SetRegistryData(Registry.CurrentUser, startupRegistryPath, 
                "FAP", string.Empty);
        RaisePropertyChanged("RunOnStartUp");
    }
    get
    {
        return (RegistryHelper.GetRegistryData(Registry.CurrentUser, 
            startupRegistryPath + "/FAP") == GetStartupCommand());
    }
}
```

### Protocol Registration

```csharp
public void RegisterProtocol()
{
    registerProtocolService.RegisterProtocol("fap", 
        Assembly.GetEntryAssembly().Location + " \"%1\"");
}
```

## Configuration Best Practices

### Default Values

- **Conservative Limits**: Start with low transfer limits
- **User-Friendly Paths**: Use desktop for downloads
- **Automatic Discovery**: Generate nicknames from system
- **Graceful Degradation**: Fallback to safe defaults

### Validation Rules

- **Required Fields**: Nickname must be provided
- **Positive Numbers**: Transfer limits must be positive
- **Valid Paths**: Directories must exist or be creatable
- **Size Limits**: Reasonable limits for system resources

### Migration Strategy

- **Backward Compatibility**: Support legacy configuration formats
- **Automatic Migration**: Convert old formats to new
- **Data Preservation**: Maintain user settings during updates
- **Error Recovery**: Fallback to defaults on corruption 