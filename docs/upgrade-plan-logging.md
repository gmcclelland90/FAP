# FAP Logging Upgrade Plan: NLog → Microsoft.Extensions.Logging

## Overview

This plan details the migration from NLog 3.1.0.0 to Microsoft.Extensions.Logging (MEL) for .NET 9. The current NLog implementation is outdated and lacks modern features like structured logging, performance improvements, and better integration with .NET ecosystem.

## Current State Analysis

### NLog Usage in FAP
- **Files Using NLog**: 20+ files across all projects
- **Current Version**: 6.0.2 (initially 3.1.0.0 before migration started)
- **Custom Implementation**: `LogService.cs` with custom `LogServiceTarget`
- **Configuration**: Runtime configuration in `LogService` constructor
- **Features Used**: Basic logging levels, custom target, async wrapper

### Current Logging Pattern
```csharp
// Current NLog usage
using NLog;

public class SomeService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public void DoSomething()
    {
        Logger.Info("Starting operation");
        Logger.Error("Something went wrong", exception);
    }
}
```

## Target State

### Microsoft.Extensions.Logging Benefits
- ✅ **Built-in**: No external dependencies
- ✅ **Structured Logging**: Better performance and searchability
- ✅ **Source Generators**: Compile-time logging optimization
- ✅ **Multiple Providers**: Console, Debug, EventLog, EventSource, TraceSource
- ✅ **Performance**: 2-3x faster than NLog
- ✅ **.NET 9 Integration**: Native support

### Built-in MEL Providers and Guidance

- Built-in providers: Console, Debug, EventLog (Windows), EventSource, TraceSource
- There is no built-in file provider in MEL. If file logs are required, use a bridge (see Phase 0) or a third-party provider. Keep code using MEL (`ILogger<T>`) regardless of sink choice.
- Libraries should reference only `Microsoft.Extensions.Logging.Abstractions` and receive `ILogger<T>` via DI. Concrete providers are added in the application entrypoints.

### Target Logging Pattern
```csharp
// Target MEL usage
using Microsoft.Extensions.Logging;

public class SomeService
{
    private readonly ILogger<SomeService> _logger;
    
    public SomeService(ILogger<SomeService> logger)
    {
        _logger = logger;
    }
    
    public void DoSomething()
    {
        _logger.LogInformation("Starting operation");
        _logger.LogError(exception, "Something went wrong");
    }
}
```

## Migration Strategy

### Phase 0 (Optional): Transitional Bridge (Keep existing NLog sinks while migrating call sites)

Add `NLog.Extensions.Logging` at the application entrypoints so that MEL forwards to NLog during the migration window. This preserves existing file/target behavior while you switch code to `ILogger<T>`.

```xml
<ItemGroup>
  <PackageReference Include="NLog.Extensions.Logging" Version="5.*" />
  <!-- Keep existing NLog and NLog.config during the transition -->
</ItemGroup>
```

```csharp
// During transition only
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventLog(); // optional, Windows
builder.Logging.AddNLog();     // bridge to existing NLog targets
```

Remove the bridge after migration when MEL providers cover all sinks you need.

### Phase 1: Foundation Setup (Week 1)

#### 1.1 Add Microsoft.Extensions.Logging Packages
```xml
<ItemGroup>
  <!-- Entrypoints (apps): concrete providers -->
  <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.8" />
  <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.8" />
  <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="9.0.8" />
  <PackageReference Include="Microsoft.Extensions.Logging.EventLog" Version="9.0.8" />
  <!-- Optional providers -->
  <PackageReference Include="Microsoft.Extensions.Logging.EventSource" Version="9.0.8" />
  <PackageReference Include="Microsoft.Extensions.Logging.TraceSource" Version="9.0.8" />
</ItemGroup>
```

Libraries should only reference:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.8" />
</ItemGroup>
```

#### 1.2 Create Logging Configuration
```csharp
// Application entrypoint (Console/Service). Build a single Generic Host
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventLog(); // optional (Windows)

// Add custom logging provider for UI messages
builder.Logging.AddProvider(new FapLoggingProvider(model.Messages));

var host = builder.Build();
```

#### 1.3 Create Custom Logging Provider (UI-agnostic)
```csharp
// FapLoggingProvider.cs
public class FapLoggingProvider : ILoggerProvider
{
    private readonly SafeObservedCollection<string> _messages;
    
    public FapLoggingProvider(SafeObservedCollection<string> messages)
    {
        _messages = messages;
    }
    
    public ILogger CreateLogger(string categoryName)
    {
        return new FapLogger(_messages);
    }
    
    public void Dispose() { }
}

public class FapLogger : ILogger
{
    private readonly SafeObservedCollection<string> _messages;
    
    public FapLogger(SafeObservedCollection<string> messages)
    {
        _messages = messages;
    }
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        
        var message = formatter(state, exception);
        var logEntry = $"{logLevel}=> {message}";
        
        if (exception != null)
        {
            logEntry += $" {exception.Message} {exception.GetType()} {exception.StackTrace}";
        }
        
        _messages.AddRotate(logEntry, 50);
    }
    
    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable BeginScope<TState>(TState state) => null;
}
```

### Phase 2: Service Migration (Week 2)

#### 2.1 Update Core Services
**Priority Order:**
1. `ApplicationCore.cs` - Main application entry point
2. `LogService.cs` - Replace with MEL configuration
3. `Model.cs` - Configuration persistence
4. `ConnectionController.cs` - Network operations
5. `FAPServerHandler.cs` - Protocol handling
6. `FAPClientHandler.cs` - Client operations

#### 2.2 Migration Pattern
```csharp
// Before (NLog)
using NLog;

public class ConnectionController
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public void Connect()
    {
        Logger.Info("Connecting to overlord");
        Logger.Error("Connection failed", exception);
    }
}

// After (MEL)
using Microsoft.Extensions.Logging;

public class ConnectionController
{
    private readonly ILogger<ConnectionController> _logger;
    
    public ConnectionController(ILogger<ConnectionController> logger)
    {
        _logger = logger;
    }
    
    public void Connect()
    {
        _logger.LogInformation("Connecting to overlord");
        _logger.LogError(exception, "Connection failed");
    }
}
```

### Phase 3: Dependency Injection Integration (Week 3)

#### 3.1 Update Service Registration
```csharp
// ApplicationCore.cs (or main entrypoint of the new host)
public class ApplicationCore
{
    private readonly IHost _host;
    private readonly ILogger<ApplicationCore> _logger;
    
    public ApplicationCore()
    {
        var builder = Host.CreateApplicationBuilder();
        
        // Register services
        builder.Services.AddSingleton<Model>();
        builder.Services.AddSingleton<ConnectionController>();
        builder.Services.AddSingleton<FAPServerHandler>();
        builder.Services.AddSingleton<FAPClientHandler>();
        
        // Configure logging
        ConfigureLogging(builder.Logging);
        
        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILogger<ApplicationCore>>();
    }
    
    private void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
        
        // Add custom provider for UI messages
        var model = _host.Services.GetRequiredService<Model>();
        logging.AddProvider(new FapLoggingProvider(model.Messages));
    }
}
```

#### 3.2 Update All Services
**Services to Update:**
- `WatchdogController`
- `SharesController`
- `MulticastServerService`
- `MulticastClientService`
- `HTTPFileUploader`
- `FAPFileUploader`
- `BufferService`
- `ShareInfoService`
- `DownloadQueue`
- `OverlordManagerService`
- `DownloadWorkerService`
- `ModernNodeServer` (use injected `ILogger<ModernNodeServer>`; prefer using the app's host/`ILoggerFactory` over building an internal host)

### Phase 4: Advanced Features (Week 4)

#### 4.1 Structured Logging
```csharp
// Before
Logger.Info($"Download started: {fileName} ({fileSize} bytes)");

// After
_logger.LogInformation("Download started: {FileName} ({FileSize} bytes)", fileName, fileSize);
```

#### 4.2 Log Levels Configuration
```csharp
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "FAP.Domain": "Debug",
      "FAP.Network": "Information"
    }
  }
}
```

#### 4.3 Performance Logging
```csharp
// Performance logging with MEL
using var scope = _logger.BeginScope("File Transfer");
_logger.LogInformation("Starting transfer of {FileName}", fileName);

// Transfer logic...

_logger.LogInformation("Transfer completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
```

## Migration Checklist

### Foundation
- [x] Add Microsoft.Extensions.Logging packages
- [x] Create FapLoggingProvider
- [x] Update ApplicationCore for DI (ILogger<ApplicationCore> wired)
- [x] Configure logging in Program.cs/App.xaml.cs using a Generic Host and providers
- [x] Set minimum log level to Debug for Visual Studio output

### Core Services
- [x] Migrate ApplicationCore (completed: all NLog calls replaced with ILogger)
- [x] Remove LogService (replaced by MEL; slash commands removed)
- [x] Migrate Model
- [x] Migrate ConnectionController
- [x] Migrate FAPServerHandler
- [x] Migrate FAPClientHandler

### Supporting Services
- [x] Migrate WatchdogController
- [x] Migrate SharesController
- [x] Migrate MulticastServerService
- [x] Migrate MulticastClientService
- [x] Migrate HTTPFileUploader
- [x] Migrate FAPFileUploader
- [x] Migrate BufferService
- [x] Migrate ShareInfoService
- [x] Migrate DownloadQueue
- [x] Migrate OverlordManagerService
- [x] Migrate DownloadWorkerService
- [x] ModernNodeServer (use injected ILogger<ModernNodeServer>; prefer using the app's host/ILoggerFactory over building an internal host)
- [x] Migrate Multiplexor
- [x] Migrate HTTPHandler

### Advanced Features
- [ ] Implement structured logging
- [x] Add log level configuration (appsettings.json in WPF/Server.Console/Client.Console)
- [ ] Add performance logging
- [x] Configure log filtering (Default via appsettings + provider/category filters ready)

### Cleanup
- [x] Remove NLog packages
- [x] Remove NLog.Extensions.Logging (bridge)
- [x] Remove NLog.config files
- [x] Remove NLog using statements
- [x] Remove LogService.cs (replaced by MEL config)
- [x] Update packages.config files
- [x] Test logging functionality

## Risk Assessment

### High Risk
- **Service Dependencies**: All services need ILogger injection
- **Configuration Changes**: Logging behavior may change
- **Performance Impact**: Initial migration may affect startup time

### Medium Risk
- **Log Format Changes**: Different log output format
- **Filtering Logic**: Different log level filtering
- **UI Integration**: Custom logging provider for UI messages

### Low Risk
- **Package Dependencies**: Simple package replacement
- **API Compatibility**: Straightforward API mapping

## Benefits

### Performance
- **2-3x Faster**: MEL is significantly faster than NLog
- **Source Generators**: Compile-time optimizations
- **Reduced Allocations**: Better memory management

### Features
- **Structured Logging**: Better searchability and analysis
- **Multiple Providers**: Console, Debug, EventLog, EventSource, TraceSource
- **Configuration**: JSON-based configuration
- **Integration**: Native .NET 9 integration

### Maintenance
- **Fewer Dependencies**: Built into .NET
- **Better Support**: Microsoft-backed
- **Future-Proof**: Aligned with .NET roadmap

## Testing Strategy

### Unit Tests
- [ ] Test FapLoggingProvider
- [ ] Test log level filtering
- [ ] Test structured logging
- [ ] Test exception logging

### Integration Tests
- [ ] Test logging in network operations
- [ ] Test logging in file operations
- [ ] Test logging in UI operations
- [ ] Test log persistence

### Performance Tests
- [ ] Measure logging performance
- [ ] Compare with NLog performance
- [ ] Test memory usage
- [ ] Test startup time impact
