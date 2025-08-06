# User Interface Architecture

## Overview

The FAP user interface is built using the Model-View-ViewModel (MVVM) pattern with WPF, providing a clean separation between presentation logic and business logic. The architecture uses dependency injection for component management and follows modern UI development practices.

## Architecture Overview

### MVVM Pattern Implementation

```
┌─────────────────────────────────────┐
│              View                   │  ← XAML UI Components
│         (WPF Controls)              │
├─────────────────────────────────────┤
│           ViewModel                 │  ← Presentation Logic
│      (Data Binding & Commands)      │
├─────────────────────────────────────┤
│            Controller               │  ← Business Logic
│      (Application Logic)            │
├─────────────────────────────────────┤
│              Model                  │  ← Data & Domain Logic
│      (Domain Entities)              │
└─────────────────────────────────────┘
```

### Dependency Injection Structure

```
┌─────────────────────────────────────┐
│         ApplicationCore             │  ← Main Application
├─────────────────────────────────────┤
│         Container (Autofac)        │  ← DI Container
├─────────────────────────────────────┤
│         Controllers                 │  ← Business Logic
├─────────────────────────────────────┤
│         ViewModels                  │  ← Presentation Logic
├─────────────────────────────────────┤
│         Views (WPF)                │  ← UI Components
└─────────────────────────────────────┘
```

## Core Components

### ApplicationCore

The main application coordinator:

```csharp
public class ApplicationCore
{
    private readonly IContainer container;
    private readonly Model model;
    private readonly ConnectionController connectionController;
    private readonly PopupWindowController popupController;
    private readonly MainWindowViewModel mainWindowModel;
    
    public ApplicationCore(IContainer c)
    {
        container = c;
        model = container.Resolve<Model>();
        connectionController = c.Resolve<ConnectionController>();
        // Initialize other components...
    }
    
    public bool Load(bool isServer) { /* ... */ }
    public void StartClient() { /* ... */ }
    public void StartOverlordServer() { /* ... */ }
}
```

### Dependency Injection Modules

#### ApplicationModule

Registers application-level components:

```csharp
public class ApplicationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ConversationController>()
               .As<IConversationController>()
               .SingleInstance();
        builder.RegisterType<PopupWindowController>()
               .SingleInstance();
        builder.RegisterType<ConnectionController>()
               .SingleInstance();
        builder.RegisterType<WatchdogController>()
               .SingleInstance();
        builder.RegisterType<ApplicationCore>()
               .SingleInstance();
    }
}
```

#### GUIModule

Registers WPF UI components:

```csharp
public class GUIModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MainWindow>().As<IMainWindow>();
        builder.RegisterType<MessageBox>().As<IMessageBoxView>();
        builder.RegisterType<DownloadQueue>().As<IDownloadQueue>();
        builder.RegisterType<SettingsPanel>().As<ISettingsView>();
        builder.RegisterType<TabWindow>().As<IPopupWindow>();
        builder.RegisterType<BrowsePanel>().As<IBrowserView>();
        builder.RegisterType<Conversation>().As<IConverstationView>();
        // Register other UI components...
    }
}
```

## View Layer

### View Interfaces

All views implement the `IView` interface:

```csharp
public interface IView
{
    object DataContext { get; set; }
}
```

### Main Window

The primary application window:

```csharp
public partial class MainWindow : Window, IMainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        // Position window
        Left = SystemParameters.PrimaryScreenWidth - Width - 50;
        Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
    }
    
    public Dispatcher Dispatcher { get { return this.Dispatcher; } }
    public void Show() { base.Show(); }
    public void Close() { base.Close(); }
    public void Flash() { /* Flash window */ }
}
```

### Panel Components

#### BrowsePanel

File browsing interface:

```csharp
public partial class BrowsePanel : UserControl, IBrowserView
{
    public BrowsePanel()
    {
        InitializeComponent();
        this.DataContextChanged += BrowsePanel_DataContextChanged;
    }
    
    private void BrowsePanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Handle data context changes
    }
}
```

#### Conversation Panel

Chat interface:

```csharp
public partial class Conversation : UserControl, IConverstationView
{
    public Conversation()
    {
        InitializeComponent();
        this.DataContextChanged += Conversation_DataContextChanged;
    }
    
    private void inputText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.IsDown)
        {
            if (null != Model)
            {
                Model.CurrentChatMessage = chatTextBox.Text;
                Model.SendChatMessage.Execute(Model);
            }
        }
    }
}
```

## ViewModel Layer

### Base ViewModel

All ViewModels inherit from the WAF framework:

```csharp
public abstract class ViewModel<TView> : ViewModel where TView : IView
{
    private readonly TView view;
    
    protected ViewModel(TView view) : base(view, false)
    {
        this.view = view;
    }
    
    protected TView ViewCore { get { return view; } }
}
```

### MainWindowViewModel

Primary application ViewModel:

```csharp
public class MainWindowViewModel : ViewModel<IMainWindow>
{
    private Model model;
    private SafeObservingCollection<Node> peers;
    private SafeObservingCollection<TransferSession> sessions;
    private string currentChatMessage;
    private string windowTitle;
    
    public Model Model
    {
        get { return model; }
        set
        {
            model = value;
            RaisePropertyChanged("Model");
        }
    }
    
    public ICommand SendChatMessage { get; set; }
    public ICommand ViewShare { get; set; }
    public ICommand Settings { get; set; }
    public ICommand EditShares { get; set; }
}
```

### Specialized ViewModels

#### ConversationViewModel

Chat functionality:

```csharp
public class ConversationViewModel : ViewModel<IConverstationView>
{
    private Conversation conversation;
    private string currentChatMessage;
    private ICommand sendChatMessage;
    
    public Conversation Conversation
    {
        get { return conversation; }
        set
        {
            conversation = value;
            RaisePropertyChanged("Conversation");
            value.UIMessages.CollectionChanged += UIMessages_CollectionChanged;
        }
    }
    
    public ICommand SendChatMessage
    {
        get { return sendChatMessage; }
        set
        {
            sendChatMessage = value;
            RaisePropertyChanged("SendChatMessage");
        }
    }
}
```

#### DownloadQueueViewModel

Download management:

```csharp
public class DownloadQueueViewModel : ViewModel<IDownloadQueue>
{
    private SafeObservingCollection<DownloadRequest> downloadQueue;
    private SafeObservingCollection<TransferLog> completedDownloads;
    private ICommand removeSelection;
    private ICommand moveup;
    private ICommand movedown;
    
    public SafeObservingCollection<DownloadRequest> DownloadQueue
    {
        get { return downloadQueue; }
        set
        {
            downloadQueue = value;
            RaisePropertyChanged("DownloadQueue");
        }
    }
    
    public ICommand RemoveSelection
    {
        get { return removeSelection; }
        set
        {
            removeSelection = value;
            RaisePropertyChanged("RemoveSelection");
        }
    }
}
```

## Controller Layer

### Controller Base

Controllers handle business logic and coordinate between ViewModels and Models:

```csharp
public abstract class AsyncControllerBase
{
    private readonly Queue<DelegateCommand> workQueue = new Queue<DelegateCommand>();
    private readonly object sync = new object();
    private bool processing = false;
    
    protected void QueueWork(DelegateCommand work)
    {
        lock (sync)
        {
            workQueue.Enqueue(work);
            if (!processing)
            {
                processing = true;
                ThreadPool.QueueUserWorkItem(ProcessQueue);
            }
        }
    }
    
    private void ProcessQueue(object o)
    {
        while (true)
        {
            DelegateCommand work = null;
            lock (sync)
            {
                if (workQueue.Count > 0)
                    work = workQueue.Dequeue();
                else
                {
                    processing = false;
                    break;
                }
            }
            work.Execute();
        }
    }
}
```

### Specialized Controllers

#### SharesController

Manages file share configuration:

```csharp
public class SharesController : AsyncControllerBase
{
    private readonly IContainer container;
    private readonly Model model;
    private readonly ShareInfoService scanner;
    private SharesViewModel viewModel;
    
    public void Initalise()
    {
        viewModel = container.Resolve<SharesViewModel>();
        viewModel.AddCommand = new DelegateCommand(AddCommand);
        viewModel.RefreshCommand = new DelegateCommand(RefreshCommand);
        viewModel.RemoveCommand = new DelegateCommand(RemoveCommand);
        viewModel.RenameCommand = new DelegateCommand(RenameCommand);
        viewModel.Shares = new SafeObservingCollection<Share>(model.Shares);
    }
    
    private void AddCommand()
    {
        string folder = string.Empty;
        if (browser.SelectFolder(out folder))
        {
            // Add new share logic
            var s = new Share();
            s.Name = name;
            s.Path = folder;
            model.Shares.Add(s);
            ThreadPool.QueueUserWorkItem(AsyncRefresh, s);
        }
    }
}
```

#### SearchController

Handles file search functionality:

```csharp
public class SearchController
{
    private readonly IContainer container;
    private readonly Model model;
    private SearchViewModel viewModel;
    
    public void Initalize()
    {
        viewModel = container.Resolve<SearchViewModel>();
        viewModel.Search = new DelegateCommand(Search);
        viewModel.Download = new DelegateCommand(Download);
        viewModel.ViewShare = new DelegateCommand(ViewShare);
        viewModel.Reset = new DelegateCommand(Reset);
    }
    
    private void Search()
    {
        // Perform search across network
        // Update ViewModel with results
    }
}
```

#### ConversationController

Manages chat conversations:

```csharp
public class ConversationController : IConversationController
{
    private readonly SafeObservedCollection<Conversation> conversations;
    private readonly List<ConversationViewModel> viewModels;
    private readonly PopupWindowController windowController;
    
    public bool HandleMessage(string id, string nickname, string message)
    {
        Node peer = model.Network.Nodes.Where(p => p.ID == id).FirstOrDefault();
        if (null != peer)
        {
            Conversation conv = conversations.Where(c => c.OtherParty == peer).FirstOrDefault();
            if (null == conv)
            {
                conv = new Conversation();
                conv.OtherParty = peer;
                conversations.Add(conv);
            }
            conv.Messages.Add(peer.Nickname + ": " + message);
            return true;
        }
        return false;
    }
}
```

## Data Binding

### Property Change Notification

All ViewModels implement `INotifyPropertyChanged`:

```csharp
public abstract class ViewModel : Model
{
    protected void RaisePropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

### Observable Collections

Safe observing collections for UI updates:

```csharp
public class SafeObservingCollection<T> : ObservableCollection<T>
{
    private readonly SafeObservedCollection<T> source;
    
    public SafeObservingCollection(SafeObservedCollection<T> source)
    {
        this.source = source;
        // Subscribe to source changes
        source.CollectionChanged += Source_CollectionChanged;
    }
    
    private void Source_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Synchronize with source collection
    }
}
```

## Command Pattern

### DelegateCommand

WPF command implementation:

```csharp
public class DelegateCommand : ICommand
{
    private readonly Action<object> execute;
    private readonly Predicate<object> canExecute;
    
    public DelegateCommand(Action<object> execute, Predicate<object> canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }
    
    public bool CanExecute(object parameter)
    {
        return canExecute == null || canExecute(parameter);
    }
    
    public void Execute(object parameter)
    {
        execute(parameter);
    }
    
    public event EventHandler CanExecuteChanged;
}
```

### Command Usage

```csharp
// In ViewModel
public ICommand SendChatMessage
{
    get { return sendChatMessage; }
    set
    {
        sendChatMessage = value;
        RaisePropertyChanged("SendChatMessage");
    }
}

// In Controller
viewModel.SendChatMessage = new DelegateCommand(SendChatMessage);

private void SendChatMessage(object parameter)
{
    // Handle chat message sending
    var vm = parameter as ConversationViewModel;
    if (null != vm && !string.IsNullOrEmpty(vm.CurrentChatMessage))
    {
        // Send message logic
    }
}
```

## Popup Window Management

### PopupWindowController

Manages modal and popup windows:

```csharp
public class PopupWindowController
{
    private readonly List<PopUpWindowTab> tabs = new List<PopUpWindowTab>();
    private readonly PopupWindowViewModel viewModel;
    
    public void AddWindow(object view, string title)
    {
        var tab = new PopUpWindowTab
        {
            View = view,
            Title = title
        };
        tabs.Add(tab);
        viewModel.DocumentViews.Add(tab);
        viewModel.ActiveDocumentView = tab;
    }
    
    public void SwitchToTab(PopUpWindowTab tab)
    {
        viewModel.ActiveDocumentView = tab;
    }
}
```

### Tab Management

```csharp
public class PopUpWindowTab
{
    public object View { get; set; }
    public string Title { get; set; }
    public bool IsSelected { get; set; }
    public ICommand CloseCommand { get; set; }
}
```

## Theme and Styling

### Resource Dictionaries

```xml
<Application.Resources>
    <local:BinaryImageConverter x:Key="imageConverter" />
    <local:SizeConverter x:Key="sizeConverter" />
    <local:SpeedConverter x:Key="speedConverter" />
    
    <LinearGradientBrush x:Key="TextBoxBorder" EndPoint="0,20" MappingMode="Absolute" StartPoint="0,0">
        <GradientStop Color="#ABADB3" Offset="0.05"/>
        <GradientStop Color="#E2E3EA" Offset="0.07"/>
        <GradientStop Color="#E3E9EF" Offset="1"/>
    </LinearGradientBrush>
</Application.Resources>
```

### Custom Controls

```csharp
public class AutoScrollListBox : ListBox
{
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.Register("AutoScroll", typeof(bool), typeof(AutoScrollListBox));
    
    public bool AutoScroll
    {
        get { return (bool)GetValue(AutoScrollProperty); }
        set { SetValue(AutoScrollProperty, value); }
    }
    
    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        base.OnItemsSourceChanged(oldValue, newValue);
        if (AutoScroll && Items.Count > 0)
            ScrollIntoView(Items[Items.Count - 1]);
    }
}
```

## Error Handling

### Exception Handling

```csharp
private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
{
    Win32Exception w32e = e.Exception as Win32Exception;
    if ((w32e != null) && (w32e.NativeErrorCode == 0))
    {
        // Ignore focus-related exceptions
        e.Handled = true;
    }
    else
    {
        LogManager.GetLogger("faplog").Fatal("Unhandled exception", e.Exception);
        e.Handled = true;
    }
}
```

### Validation

```csharp
public class SettingsViewModel : ViewModel<ISettingsView>, IDataErrorInfo
{
    public string this[string columnName]
    {
        get
        {
            switch (columnName)
            {
                case "Nickname":
                    if (string.IsNullOrEmpty(Nickname))
                        return "Nickname is required";
                    break;
            }
            return null;
        }
    }
    
    public string Error { get { return null; } }
}
```

## Performance Optimizations

### Virtualization

```xml
<ListBox ItemsSource="{Binding Items}" 
         VirtualizingStackPanel.IsVirtualizing="True"
         VirtualizingStackPanel.VirtualizationMode="Recycling">
```

### Async Operations

```csharp
private void RefreshAsync()
{
    ThreadPool.QueueUserWorkItem(delegate
    {
        // Perform background work
        Dispatcher.Invoke(() =>
        {
            // Update UI on main thread
            UpdateUI();
        });
    });
}
```

### Memory Management

```csharp
public void Dispose()
{
    // Clean up resources
    if (conversations != null)
    {
        foreach (var conv in conversations)
            conv.Dispose();
    }
    
    if (viewModels != null)
    {
        foreach (var vm in viewModels)
            vm.Dispose();
    }
}
```

## Testing Support

### Unit Testing

```csharp
[Test]
public void TestViewModelPropertyChange()
{
    var viewModel = new TestViewModel();
    bool propertyChanged = false;
    
    viewModel.PropertyChanged += (sender, e) =>
    {
        if (e.PropertyName == "TestProperty")
            propertyChanged = true;
    };
    
    viewModel.TestProperty = "New Value";
    Assert.IsTrue(propertyChanged);
}
```

### Mock Views

```csharp
public class MockView : IView
{
    public object DataContext { get; set; }
}

[Test]
public void TestViewModelDataContext()
{
    var mockView = new MockView();
    var viewModel = new TestViewModel(mockView);
    
    Assert.AreEqual(viewModel, mockView.DataContext);
}
```

## Accessibility

### Keyboard Navigation

```csharp
private void inputText_KeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Enter && e.IsDown)
    {
        // Handle Enter key for sending messages
        SendMessage();
        e.Handled = true;
    }
}
```

### Screen Reader Support

```xml
<Button Content="Send" 
        AutomationProperties.Name="Send Message"
        AutomationProperties.HelpText="Click to send the current message" />
```

## Internationalization

### Resource Management

```csharp
public static class Localization
{
    public static string GetString(string key)
    {
        return Properties.Resources.ResourceManager.GetString(key);
    }
}
```

### Culture Support

```csharp
public class CultureAwareConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Culture-aware conversion logic
        return value;
    }
}
``` 