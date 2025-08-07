# FAP Project Migration Plan: .NET Framework 4.0 → .NET 9

## Project Overview

**FAP (File Access Protocol)** is a HTTP-based LAN file sharing tool with the following features:
- Automatic LAN peer discovery
- File browsing and downloading (client + web browser)
- Chat functionality
- System specification comparison
- Network file search
- File transfer visualization
- Upload/download limiting and queuing
- Statistics and monitoring
- Unicode filename support
- Large file support (up to 16TB)

**Current Architecture:**
- **Framework**: .NET Framework 4.0
- **UI**: WPF (Windows Presentation Foundation)
- **Dependency Injection**: Autofac 2.3.2
- **Logging**: NLog 3.1.0
- **JSON**: Newtonsoft.Json.Net35
- **Protocol Buffers**: protobuf-net
- **HTTP Server**: Custom HttpServer library
- **WPF Framework**: WpfApplicationFramework
- **UI Controls**: Odyssey (custom controls)
- **WMI Access**: LinqToWmi
- **String Templates**: ~~StringTemplate (Antlr3)~~ ✅ **COMPLETED** - Replaced with custom TemplateEngine

## Migration Strategy

### Phase 1: Project Structure Modernization

#### 1.1 Convert to SDK-Style Projects
- Convert all `.csproj` files from legacy format to SDK-style
- Update target framework from `net40` to `net9.0`
- Remove old MSBuild properties and use modern SDK properties
- Update project references to use PackageReference format

**Files to update:**
- `FAP.Domain/FAP.Domain.csproj`
- `FAP.Application/FAP.Application.csproj`
- `FAP.Foundation/FAP.Foundation.csproj`
- `FAP.Network/FAP.Network.csproj`
- `UI/Client.WPF/Fap.Presentation.csproj`
- `UI/Client.Console/Client.Console.csproj`
- `UI/Server.Console/Server.Console.csproj`
- All library projects in `libs/` directory

#### 1.2 Update Solution File
- Update `Fap.sln` to use modern Visual Studio format
- Remove old project configurations
- Update build configurations for .NET 9

### Phase 2: Dependency Updates

#### 2.1 Core Dependencies
- **Autofac**: Update from 2.3.2 to latest (6.x)
- **NLog**: Update from 3.1.0 to latest (5.x)
- **Newtonsoft.Json**: Replace with System.Text.Json (built into .NET 9)
- **protobuf-net**: Update to latest version compatible with .NET 9

#### 2.2 Library Modernization
- **HttpServer**: Replace with ASP.NET Core minimal APIs
- **WpfApplicationFramework**: Replace with modern WPF patterns or migrate to .NET MAUI
- **Odyssey**: Modernize custom controls or replace with modern alternatives
- **LinqToWmi**: Replace with modern WMI access patterns
- **StringTemplate**: ~~Replace with modern templating solutions~~ ✅ **COMPLETED** - Replaced with custom TemplateEngine

#### 2.3 Remove Obsolete Dependencies
- **Newtonsoft.Json.Net35**: Replace with System.Text.Json
- **Antlr3.Runtime**: ~~Replace StringTemplate with modern alternatives~~ ✅ **COMPLETED** - Removed Antlr3.StringTemplate dependency
- **BlogsPrajeesh.BlogSpot.WPFControls**: Replace with modern WPF controls

### Phase 3: Code Modernization

#### 3.1 Language Features
- **Nullable Reference Types**: Enable and fix nullability warnings
- **Pattern Matching**: Modernize switch expressions and pattern matching
- **Records**: Convert simple data classes to records
- **Top-level Statements**: Simplify Program.cs files
- **Async Streams**: Modernize async operations
- **Default Interface Methods**: Use where applicable

#### 3.2 API Modernization
- **System.Net**: Update to modern networking APIs
- **System.Threading**: Use modern async/await patterns
- **System.IO**: Use modern file I/O APIs
- **System.Collections**: Use modern collection types
- **System.ComponentModel**: Update to modern patterns

#### 3.3 WPF Modernization
- **XAML**: Update to modern XAML patterns
- **Data Binding**: Use modern binding patterns
- **Commands**: Update to modern command patterns
- **Styling**: Modernize styles and themes
- **Controls**: Update to modern WPF controls

### Phase 4: Architecture Improvements

#### 4.1 Dependency Injection
- **Autofac → Microsoft.Extensions.DependencyInjection**: Migrate to built-in DI
- **Service Registration**: Modernize service registration patterns
- **Configuration**: Use Microsoft.Extensions.Configuration

#### 4.2 Logging
- **NLog → Microsoft.Extensions.Logging**: Migrate to built-in logging
- **Structured Logging**: Implement structured logging patterns
- **Log Levels**: Modernize log level usage

#### 4.3 Configuration
- **app.config → appsettings.json**: Migrate to modern configuration
- **User Settings**: Use modern settings patterns
- **Environment Configuration**: Implement environment-based configuration

#### 4.4 HTTP Server Modernization
- **Custom HttpServer → ASP.NET Core**: Replace custom HTTP server
- **Minimal APIs**: Use ASP.NET Core minimal APIs
- **Middleware**: Implement modern middleware patterns
- **Static Files**: Use built-in static file serving

### Phase 5: Performance and Security

#### 5.1 Performance Improvements
- **Memory Management**: Use modern memory management patterns
- **Async Operations**: Implement proper async/await patterns
- **Caching**: Use modern caching solutions
- **Compression**: Implement modern compression

#### 5.2 Security Updates
- **Cryptography**: Update to modern cryptographic APIs
- **Authentication**: Implement modern authentication patterns
- **Authorization**: Use modern authorization patterns
- **HTTPS**: Implement proper HTTPS support

### Phase 6: Testing and Validation

#### 6.1 Unit Testing
- **Test Framework**: Update to modern testing frameworks
- **Mocking**: Use modern mocking libraries
- **Test Patterns**: Implement modern testing patterns

#### 6.2 Integration Testing
- **HTTP Testing**: Test modern HTTP endpoints
- **Database Testing**: Test data persistence
- **UI Testing**: Test modern UI components

## Detailed Migration Steps

### Step 1: Create Migration Branch
```bash
git checkout -b migration/net9-upgrade
```

### Step 2: Update Project Files
1. Convert each `.csproj` to SDK-style format
2. Update target framework to `net9.0`
3. Replace PackageReference format for dependencies
4. Remove old MSBuild properties

### Step 3: Update Dependencies
1. Update NuGet packages to .NET 9 compatible versions
2. Replace obsolete packages with modern alternatives
3. Update package references in all projects

### Step 4: Fix Breaking Changes
1. Update namespace references
2. Fix API changes
3. Update method signatures
4. Fix deprecated features

### Step 5: Modernize Code
1. Enable nullable reference types
2. Update async/await patterns
3. Use modern C# features
4. Update WPF patterns

### Step 6: Update Configuration
1. Migrate app.config to appsettings.json
2. Update configuration patterns
3. Implement modern settings

### Step 7: Test and Validate
1. Run unit tests
2. Test application functionality
3. Validate performance
4. Check security

## Risk Assessment

### High Risk
- **Custom HttpServer**: Complete rewrite required
- **WpfApplicationFramework**: May need significant updates
- **LinqToWmi**: May not be compatible with .NET 9

### Medium Risk
- **Autofac**: Migration to Microsoft.Extensions.DependencyInjection
- **NLog**: Migration to Microsoft.Extensions.Logging
- **Custom Controls**: May need modernization

### Low Risk
- **Core Business Logic**: Should migrate easily
- **Data Models**: Should be compatible
- **Basic WPF**: Should work with minimal changes

## Timeline Estimate

- **Phase 1-2**: 2-3 weeks (Project structure and dependencies)
- **Phase 3**: 3-4 weeks (Code modernization)
- **Phase 4**: 2-3 weeks (Architecture improvements)
- **Phase 5**: 1-2 weeks (Performance and security)
- **Phase 6**: 2-3 weeks (Testing and validation)

**Total Estimated Time**: 10-15 weeks

## Success Criteria

1. **Compilation**: All projects compile successfully with .NET 9
2. **Functionality**: All features work as expected
3. **Performance**: Performance is maintained or improved
4. **Security**: Security is improved with modern APIs
5. **Maintainability**: Code is more maintainable with modern patterns

## Rollback Plan

- Maintain the original .NET Framework 4.0 branch
- Keep all original dependencies and configurations
- Document all changes for potential rollback
- Test rollback procedures before starting migration

## Post-Migration Benefits

1. **Performance**: Improved runtime performance
2. **Security**: Better security with modern APIs
3. **Maintainability**: Easier to maintain with modern patterns
4. **Future-Proof**: Ready for future .NET updates
5. **Modern Features**: Access to latest C# and .NET features
6. **Cloud-Native**: Better support for cloud deployment
7. **Cross-Platform**: Potential for cross-platform deployment
