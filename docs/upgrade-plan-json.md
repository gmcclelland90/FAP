# FAP JSON Upgrade Plan: Newtonsoft.Json → System.Text.Json

## Overview

This plan details the migration from Newtonsoft.Json (Json.NET) to System.Text.Json for .NET 9. The current Json.NET implementation is outdated (version 3.1.0.0) and lacks modern performance features. System.Text.Json provides better performance, built-in support, and modern features like source generators.

## Current State Analysis

### Json.NET Usage in FAP
- **Current Version**: Newtonsoft.Json 13.0.3 (package reference in `FAP.Domain`)
- **Legacy Asset**: `libs/Json.NET/Newtonsoft.Json.Net35.dll` exists but not used by main projects
- **Files Using Json.NET**: 10+ files across domain entities and verbs
- **Usage Patterns**: Configuration persistence (`Model`, `DownloadQueue`) and protocol payloads (`BaseVerb`)
- **Features Used**: Basic JSON serialization; `[JsonIgnore]` attributes; default PascalCase naming; formatting varies by call site

### Current JSON Usage Pattern
```csharp
// Current Json.NET usage
using Newtonsoft.Json;

public class Model : BaseEntity
{
    [JsonIgnore]
    public SafeObservedCollection<TransferLog> CompletedDownloads { get; set; }
    
    public void Save()
    {
        var json = JsonConvert.SerializeObject(this);
        File.WriteAllText(saveLocation, json);
    }
    
    public void Load()
    {
        var json = File.ReadAllText(saveLocation);
        var model = JsonConvert.DeserializeObject<Model>(json);
        // Copy properties...
    }
}
```

## Target State

### System.Text.Json Benefits
- ✅ **Built-in**: No external dependencies
- ✅ **Performance**: 2-3x faster than Json.NET
- ✅ **Source Generators**: Compile-time serialization optimization
- ✅ **Memory Efficient**: Reduced allocations
- ✅ **.NET 9 Integration**: Native support
- ✅ **Modern Features**: Async serialization, streaming

### Target JSON Usage Pattern
```csharp
// Target System.Text.Json usage
using System.Text.Json;
using System.Text.Json.Serialization;

public class Model : BaseEntity
{
    [JsonIgnore]
    public SafeObservedCollection<TransferLog> CompletedDownloads { get; set; }
    
    public async Task SaveAsync()
    {
        // Preserve current on-disk shape: PascalCase property names, indented
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true
        };
        
        using var stream = File.Create(saveLocation);
        await JsonSerializer.SerializeAsync(stream, this, options);
    }
    
    public async Task LoadAsync()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null
        };
        
        using var stream = File.OpenRead(saveLocation);
        var model = await JsonSerializer.DeserializeAsync<Model>(stream, options);
        // Copy properties...
    }
}
```

## Migration Strategy

### Phase 1: Foundation Setup (Week 1)

#### 1.1 Remove Json.NET Dependencies
```xml
<!-- Remove from all projects -->
<PackageReference Include="Newtonsoft.Json" Version="3.1.0.0" />
```

#### 1.2 Add System.Text.Json Configuration
```csharp
// JsonConfiguration.cs
public static class JsonConfiguration
{
    // Preserve existing persisted files: PascalCase, include nulls, indented
    public static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true
    };

    // For compact files (e.g., download queue): PascalCase, no indentation
    public static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true
    };
}
```

#### 1.3 Create Migration Helper (optional)
```csharp
// JsonMigrationHelper.cs
public static class JsonMigrationHelper
{
    public static T? DeserializeLenient<T>(string json, JsonSerializerOptions options)
    {
        // Case-insensitive, PascalCase preserved via options
        return JsonSerializer.Deserialize<T>(json, options);
    }
}
```

### Phase 2: Entity Migration (Week 2)

#### 2.1 Update BaseEntity (persistence helpers)
```csharp
// BaseEntity.cs: switch to System.Text.Json for SafeSave/SafeLoad
using System.Text.Json;
using System.Text.Json.Serialization;

// Replace Newtonsoft.Json usage with System.Text.Json and reuse
// JsonConfiguration.IndentedOptions / CompactOptions at call sites
```

#### 2.2 Update Model Entity
```csharp
// Model.cs
using System.Text.Json.Serialization;

public class Model : BaseEntity
{
    [JsonIgnore]
    public SafeObservedCollection<TransferLog> CompletedDownloads { get; set; }
    
    [JsonIgnore]
    public SafeObservingCollection<TransferLog> UICompletedDownloads { get; set; }
    
    [JsonPropertyName("localNode")]
    public Node LocalNode { get; set; }
    
    [JsonPropertyName("shares")]
    public SafeObservedCollection<Share> Shares { get; set; }
    
    [JsonPropertyName("downloadQueue")]
    public DownloadQueue DownloadQueue { get; set; }
    
    public async Task SaveAsync()
    {
        var options = JsonConfiguration.IndentedOptions;
        using var stream = File.Create(saveLocation);
        await JsonSerializer.SerializeAsync(stream, this, options);
    }
    
    public async Task LoadAsync()
    {
        if (!File.Exists(saveLocation)) return;
        
        var options = JsonConfiguration.IndentedOptions;
        using var stream = File.OpenRead(saveLocation);
        var model = await JsonSerializer.DeserializeAsync<Model>(stream, options);
        
        // Copy properties to maintain object references
        CopyProperties(model);
    }
    
    private void CopyProperties(Model source)
    {
        // Copy all properties while maintaining object references
        LocalNode = source.LocalNode;
        Shares = source.Shares;
        DownloadQueue = source.DownloadQueue;
        // ... other properties
    }
}
```

#### 2.3 Update NetworkRequest (if serialized)
```csharp
// NetworkRequest.cs
using System.Text.Json.Serialization;

public class NetworkRequest
{
    [JsonPropertyName("verb")]
    public string Verb { get; set; }
    
    [JsonPropertyName("data")]
    public string Data { get; set; }
    
    [JsonPropertyName("param")]
    public string Param { get; set; }
    
    [JsonPropertyName("sourceId")]
    public string SourceID { get; set; }
    
    [JsonPropertyName("overlordId")]
    public string OverlordID { get; set; }
    
    [JsonPropertyName("authKey")]
    public string AuthKey { get; set; }
}
```

### Phase 3: Service Migration (Week 3)

#### 3.1 Update BaseVerb protocol payloads
```csharp
// BaseVerb.cs → switch Serialize/Deserialize to System.Text.Json
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = null };
JsonSerializer.Serialize(obj, options);
JsonSerializer.Deserialize<T>(json, options);
```

#### 3.2 Update Template Engine (optional)
```csharp
// TemplateEngine.cs
using System.Text.Json;

public class TemplateEngine
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public string RenderTemplate(string template, object model)
    {
        var json = JsonSerializer.Serialize(model, Options);
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(json);
        
        return ReplaceTemplateVariables(template, jsonObject);
    }
    
    private string ReplaceTemplateVariables(string template, JsonElement json)
    {
        // Replace $variable$ with JSON values
        var result = template;
        
        foreach (var property in json.EnumerateObject())
        {
            var placeholder = $"${property.Name}$";
            var value = property.Value.GetString() ?? property.Value.ToString();
            result = result.Replace(placeholder, value);
        }
        
        return result;
    }
}
```

### Phase 4: Performance Optimization (Week 4)

#### 4.1 Source Generator Configuration
```csharp
// JsonContext.cs
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(Model))]
[JsonSerializable(typeof(NetworkRequest))]
[JsonSerializable(typeof(DownloadRequest))]
[JsonSerializable(typeof(Share))]
[JsonSerializable(typeof(Node))]
public partial class FapJsonContext : JsonSerializerContext
{
}
```

#### 4.2 Optimized Serialization
```csharp
// OptimizedModel.cs
public partial class Model
{
    public async Task SaveAsync()
    {
        using var stream = File.Create(saveLocation);
        await JsonSerializer.SerializeAsync(stream, this, FapJsonContext.Default.Model);
    }
    
    public async Task LoadAsync()
    {
        if (!File.Exists(saveLocation)) return;
        
        using var stream = File.OpenRead(saveLocation);
        var model = await JsonSerializer.DeserializeAsync(stream, FapJsonContext.Default.Model);
        CopyProperties(model);
    }
}
```

#### 4.3 Streaming for Large Objects
```csharp
// StreamingJsonService.cs
public class StreamingJsonService
{
    public async IAsyncEnumerable<T> DeserializeStreamAsync<T>(Stream stream)
    {
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<T>(stream))
        {
            yield return item;
        }
    }
    
    public async Task SerializeStreamAsync<T>(Stream stream, IAsyncEnumerable<T> items)
    {
        await JsonSerializer.SerializeAsync(stream, items);
    }
}
```

## Migration Checklist

### Foundation
- [x] Add System.Text.Json configuration (Indented/Compact options)
- [x] Update BaseEntity persistence to System.Text.Json
- [x] Update project references
- [x] Remove Newtonsoft.Json packages (after entity/service migration)

### Entity Migration
- [x] Replace `using Newtonsoft.Json` with `using System.Text.Json.Serialization`
- [x] Update Model entity
- [ ] Update NetworkRequest entity (verify if persisted/serialized; adjust if needed)
- [ ] Update DownloadRequest entity (verify usage; likely attributes only)
- [ ] Update Share entity (verify usage; likely attributes only)
- [ ] Update Node entity (verify usage; likely attributes only)
- [ ] Update Overlord entity (attributes swapped)
- [ ] Update SearchResult entity (attributes swapped)

### Service Migration
- [x] Update BaseVerb for System.Text.Json
- [ ] Update TemplateEngine
- [x] Update configuration persistence (Model, BaseEntity, DownloadQueue)
- [x] Update protocol serialization (verbs use System.Text.Json)
- [ ] Test serialization/deserialization

### Performance Optimization
- [ ] Add source generator configuration
- [ ] Implement optimized serialization
- [ ] Add streaming support for large objects
- [ ] Configure performance settings
- [ ] Test performance improvements

### Cleanup
- [ ] Remove Json.NET using statements
- [ ] Remove Newtonsoft.Json attributes (replace with System.Text.Json)
- [ ] Update packages.config files
- [ ] Test all JSON functionality
- [ ] Performance testing

## Risk Assessment

### High Risk
- **Data Compatibility**: Existing JSON files may not deserialize correctly
- **Performance Impact**: Initial migration may affect performance
- **API Changes**: Different serialization behavior

### Medium Risk
- **Attribute Changes**: Different attribute names and behavior
- **Null Handling**: Different null handling behavior
- **Date Formatting**: Different date serialization format

### Low Risk
- **Package Dependencies**: Built into .NET
- **API Compatibility**: Well-documented APIs
- **Migration Path**: Clear migration path available

## Benefits

### Performance
- **2-3x Faster**: System.Text.Json is significantly faster than Json.NET
- **Source Generators**: Compile-time serialization optimization
- **Reduced Allocations**: Better memory management
- **Async Support**: Native async serialization

### Features
- **Built-in**: No external dependencies
- **Modern APIs**: Async, streaming, source generators
- **Configuration**: Flexible configuration options
- **Integration**: Native .NET 9 integration

### Maintenance
- **Fewer Dependencies**: Built into .NET
- **Better Support**: Microsoft-backed
- **Future-Proof**: Aligned with .NET roadmap
- **Documentation**: Extensive documentation

## Testing Strategy

### Unit Tests
- [ ] Test serialization/deserialization
- [ ] Test attribute handling
- [ ] Test null handling
- [ ] Test date formatting

### Integration Tests
- [ ] Test configuration persistence
- [ ] Test protocol serialization
- [ ] Test template engine
- [ ] Test data migration

### Performance Tests
- [ ] Measure serialization performance
- [ ] Compare with Json.NET performance
- [ ] Test memory usage
- [ ] Test async performance

### Compatibility Tests
- [ ] Test with existing JSON files
- [ ] Test with existing protocol data
- [ ] Test backward compatibility
- [ ] Test data migration

## Migration Timeline

### Week 1: Foundation
- Remove Json.NET dependencies
- Add System.Text.Json configuration
- Create migration helpers
- Set up source generators

### Week 2: Entities
- Migrate BaseEntity
- Migrate Model entity
- Migrate NetworkRequest
- Migrate other entities

### Week 3: Services
- Migrate Multiplexor
- Migrate TemplateEngine
- Migrate configuration persistence
- Test protocol compatibility

### Week 4: Optimization
- Add source generator configuration
- Implement streaming support
- Performance optimization
- Final testing and cleanup
