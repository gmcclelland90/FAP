# FAP UI Modernization Plan

## Overview

This document covers two independent improvements to FAP's user interface:

1. **Web interface**: Replace the custom `TemplateEngine` with ASP.NET Core Razor Pages for the browser-based file browsing experience.
2. **Desktop client**: Modernize the existing WPF application in place — apply the Windows 11 Fluent theme, replace Odyssey and WpfApplicationFramework with modern maintained alternatives, and clean up legacy patterns.

Both tracks can be worked in parallel and neither requires a framework rewrite.

## Current State

### Web Interface
- Custom `TemplateEngine` (`FAP.Domain/Services/TemplateEngine.cs`) using `$variable$` syntax with regex-based replacement, loops (`$collection:{item|...}$`), and conditionals (`$if:condition|...$`).
- Single HTML template (`FAP.Domain/Web.Resources/template.html`) with jQuery 1.5.2 and DataTables for sortable file listings.
- Served by `ModernHTTPHandler` via Kestrel at `/Fap.app.web/` (static files) and dynamic routes for browsing.
- Static assets: `css/fap.css`, `js/jquery-1.5.2.min.js`, `js/jquery.dataTables.min.js`, various images.

### Desktop Client (WPF)
- 19 XAML files: 10 panels, 6 windows, app, theme, and misc.
- **Odyssey**: Only used for `BreadcrumbBar` in `BrowsePanel.xaml`. One namespace import, two event handlers in code-behind. Minimal footprint.
- **WpfApplicationFramework** (`System.Waf`): Used for MVVM infrastructure — `ViewModel<T>`, `DelegateCommand`, view/controller patterns, `IMessageService`. Referenced across most ViewModels and controllers in `FAP.Application`.
- Standard WPF controls everywhere else (TreeView, ListView, TabControl, Grid, etc.).
- Panels: BrowsePanel, Conversation, DownloadQueue, SearchPanel, ComparePanel, SharesPanel, SettingsPanel, UserInfoPanel, LogPanel, WebPanel.

## Track 1: Web Interface — Razor Pages

### Why Razor Pages

The web interface is server-rendered HTML — users browse to a FAP node's address, see a file listing, and download files. This is exactly what Razor Pages does well. There is no need for a client-side SPA framework (Blazor, React, etc.) because the interaction model is simple page navigation.

Benefits over the current `TemplateEngine`:
- IntelliSense and compile-time checking for all markup.
- Strongly typed models instead of `Dictionary<string, object>` with reflection.
- Standard Razor syntax (`@Model.Property`, `@foreach`, `@if`) replaces the custom `$variable$` and `$collection:{item|...}$` syntax.
- Built-in layout pages, partial views, tag helpers.
- Hot reload during development.
- No regex-based template processing at runtime.

### Implementation Plan

#### Step 1: Add Razor Pages to Kestrel

The existing `ModernNodeServer` already runs Kestrel with `UseEndpoints`. Razor Pages can be added alongside the existing middleware.

In `ModernNodeServer.ConfigureServices`:
```csharp
services.AddRazorPages();
```

In `ModernNodeServer.Configure`:
```csharp
app.MapRazorPages();
```

#### Step 2: Create the Browse Page

Replace `template.html` + `TemplateEngine.Generate()` with a single Razor Page.

**`Pages/Browse.cshtml.cs`** — page model that replaces the `Dictionary<string, object>` data preparation in `ModernHTTPHandler`:
```csharp
public class BrowseModel : PageModel
{
    public string Nickname { get; set; }
    public string CurrentPath { get; set; }
    public List<PathSegment> PathSegments { get; set; }
    public List<BrowsingFile> Files { get; set; }
    public string TotalSize { get; set; }
    public int CurrentUploadSlots { get; set; }
    public int TotalUploadSlots { get; set; }
    public string FreeLimit { get; set; }
    public string AppVersion { get; set; }

    public void OnGet(string path) { /* populate from Model/ShareInfoService */ }
}
```

**`Pages/Browse.cshtml`** — the markup, equivalent to `template.html`:
```html
@page "{*path}"
@model BrowseModel

<!DOCTYPE html>
<html>
<head>
    <title>@Model.Nickname's Web Share</title>
    <link rel="stylesheet" href="/Fap.app.web/css/fap.css" />
</head>
<body>
    <h1>@Model.Nickname's Shares</h1>
    <p>@Model.CurrentUploadSlots of @Model.TotalUploadSlots upload slots available.</p>

    <h2>Current Path:</h2>
    <nav>
        <a href="/">Root</a>
        @foreach (var seg in Model.PathSegments)
        {
            <span class="PathSplitter">/</span>
            <a href="@seg.Path">@seg.Name</a>
        }
    </nav>

    <table class="display" id="files">
        <thead>
            <tr><th>Icon</th><th>Name</th><th>Modified</th><th>Size</th></tr>
        </thead>
        <tbody>
            @foreach (var file in Model.Files)
            {
                <tr>
                    <td>@Html.Raw(file.IconHtml)</td>
                    <td><a href="@Model.CurrentPath@file.Path">@file.Name</a></td>
                    <td>@file.LastModifiedtxt</td>
                    <td class="center">@file.Sizetxt</td>
                </tr>
            }
        </tbody>
    </table>
    <div style="text-align:center">Total Size: @Model.TotalSize</div>
    <div style="text-align:center">Page generated by @Model.AppVersion</div>
</body>
</html>
```

#### Step 3: Update Static Assets

- Replace jQuery 1.5.2 with a current version, or remove jQuery entirely and use vanilla JS or a lightweight alternative for DataTables-style sorting (e.g. `simple-datatables`).
- Modernize `fap.css` — consider a minimal CSS framework or just update the existing styles.
- Keep the existing `/Fap.app.web/` static file path so bookmarks and the FAP protocol link (`fap://`) format remain compatible.

#### Step 4: Remove TemplateEngine

Once the Razor Pages are working:
- Delete `FAP.Domain/Services/TemplateEngine.cs`.
- Remove the template-processing code path from `ModernHTTPHandler` that calls `TemplateEngine.Generate()`.
- Keep `Web.Resources/` for static assets (CSS, JS, images, favicon) but the `template.html` file is no longer needed.

#### Step 5: Consider Additional Pages

The web interface could expand beyond file browsing without much effort:
- A simple status/info page showing node details and network stats.
- A download page that accepts `fap://` links and renders them as direct HTTP downloads.

These are optional and low-priority.

### Migration Effort

Estimated 2–3 days of focused work. The template is a single page and the data model already exists.

---

## Track 2: Desktop Client — WPF Modernization

### Why Stay on WPF

- WPF is actively maintained on .NET 9 and receives updates. It is not deprecated.
- FAP is Windows-only (WMI, registry, Windows networking). There is no cross-platform target that would justify MAUI.
- The existing WPF UI works. The goal is to modernize it, not rewrite it.
- .NET 9 added built-in support for the Windows 11 Fluent theme, which gives WPF apps a modern native appearance with minimal effort.

### Phase 1: Windows 11 Fluent Theme

.NET 9 introduced `FluentTheme` as a built-in WPF theme. This gives all standard controls (Button, TextBox, ComboBox, ListView, TreeView, TabControl, etc.) a Windows 11 native look without changing any XAML.

**In `App.xaml`:**
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:FluentTheme />
            <!-- Existing resource dictionaries -->
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

This single change updates the visual appearance of every standard WPF control in the application. Test thoroughly — some custom styles in `Themes/Generic.xaml` may need adjustment to work with the new theme.

**Effort**: A few hours plus testing.

### Phase 2: Replace Odyssey

Odyssey is only used for the `BreadcrumbBar` control in `BrowsePanel.xaml`. This is a small, contained replacement.

**Options:**
1. **WPF standard controls**: Replace the breadcrumb bar with a styled `ItemsControl` or `ToolBar` with clickable path segments. This is the simplest option and avoids introducing another third-party dependency.
2. **Custom BreadcrumbBar**: Build a simple breadcrumb UserControl using a horizontal `StackPanel`/`ItemsControl` with `Button` items for each path segment. The existing code already splits the path into segments for the web template — reuse that logic.

**Steps:**
1. Create a simple `BreadcrumbBar` UserControl in the WPF project.
2. Update `BrowsePanel.xaml` to use it instead of `odc:BreadcrumbBar`.
3. Port the `PopulateItems` and `DropDownOpened` event handler logic.
4. Remove the Odyssey project reference from `Fap.Presentation.csproj`.
5. Remove `libs/Odyssey/` from the solution (can keep in repo as archive if desired).

**Effort**: 1–2 days.

### Phase 3: Replace WpfApplicationFramework

WpfApplicationFramework (`System.Waf`) provides MVVM infrastructure used across ViewModels and controllers. The modern replacement is **CommunityToolkit.Mvvm** (Microsoft-maintained, widely adopted, actively developed).

**Mapping:**

| WpfApplicationFramework | CommunityToolkit.Mvvm | Notes |
|------------------------|----------------------|-------|
| `ViewModel<TView>` | `ObservableObject` | Drop the generic view parameter; views are resolved via DI instead |
| `DelegateCommand` | `RelayCommand` / `AsyncRelayCommand` | Near-identical API; `AsyncRelayCommand` adds proper async support |
| `IMessageService` | Keep or replace with custom | Simple interface, can keep a thin wrapper |
| `DataModel` | `ObservableObject` | Base for observable entities |

**Steps:**
1. Add `CommunityToolkit.Mvvm` NuGet package to `FAP.Application`.
2. Migrate ViewModels one at a time — change base class from `ViewModel<T>` to `ObservableObject`, replace `DelegateCommand` with `RelayCommand`.
3. Update the view resolution pattern. Currently `ViewModel<TView>` ties a ViewModel to a specific view interface. With CommunityToolkit, ViewModels are plain `ObservableObject` subclasses and views are wired via DI or DataTemplates.
4. Migrate `IMessageService` — either keep the existing `System.Waf.Presentation.Services.MessageService` wrapper or replace with a simple `MessageBox.Show` wrapper.
5. Once all references are migrated, remove the WpfApplicationFramework project reference.
6. Remove `libs/WpfApplicationFramework/` from the solution.

**Effort**: 1–2 weeks (incremental, can be done ViewModel by ViewModel).

### Phase 4: Clean Up Project References

After Phases 1–3:
- Remove `<UseWPF>true</UseWPF>` from non-UI projects (`FAP.Domain`, `FAP.Network`, `FAP.Application`, `FAP.Foundation`). If any of these depend on `ObservableCollection<T>` or `DispatcherObject`, add explicit `System.ObjectModel` or refactor to use events/callbacks.
- Remove `<UseWindowsForms>true</UseWindowsForms>` from `FAP.Foundation` if only used for `FolderBrowserDialog` — consider using the WPF-native `Microsoft.Win32.OpenFolderDialog` (available since .NET 8).
- Remove `<FrameworkReference Include="Microsoft.AspNetCore.App" />` from `FAP.Domain.csproj` — move any ASP.NET Core dependencies (e.g., `Multiplexor` HTTP context types) to `FAP.Network` where they belong.
- Remove legacy WpfApplicationFramework test/snippets projects still targeting .NET 3.5 from the solution.
- Remove `Client.Console` from the solution (already replaced by integration tests).

**Effort**: A few hours of refactoring and testing.

### Phase 5: Optional Visual Polish

Once the foundation is modernized:
- **Mica/Acrylic backdrop**: .NET 9 WPF supports `WindowBackdropType` for Windows 11 transparency effects on the main window.
- **Custom accent colors**: The Fluent theme respects the user's Windows accent color automatically.
- **Dark mode**: FluentTheme supports `RequestedTheme="Dark"` or can follow the system setting.
- **Icon refresh**: Replace the existing tray/window icons with modern Fluent-style icons if desired.
- **Modernize the tab layout**: The current `TabWindow` uses a custom tab control (`Wpf.Controls.TabControl`). Consider using the standard WPF TabControl (which will pick up FluentTheme styling) or the `ModernTabWindow` that already exists.

These are cosmetic and entirely optional.

---

## Dependencies and Prerequisites

Before starting either track, the following items from `TODO.md` should ideally be completed first, as they affect both the web and desktop code paths:

- **Async modernization** — Razor Page handlers and modern WPF patterns both expect async throughout. Fixing the remaining `Thread.Sleep`, `ThreadPool.QueueUserWorkItem`, `.Result`, and `.GetAwaiter().GetResult()` calls first avoids rework.
- **IHttpClientFactory adoption** — Relevant to both service layer and any HTTP calls ViewModels make.

However, neither track is strictly blocked by these — they can proceed in parallel if needed.

## Timeline

| Track | Phase | Effort | Priority |
|-------|-------|--------|----------|
| Web | Razor Pages migration | 2–3 days | Medium |
| Web | Static asset updates | 1 day | Low |
| Web | Remove TemplateEngine | 1 hour | Low (after Razor Pages work) |
| Desktop | Fluent theme | Half day | High (quick win) |
| Desktop | Replace Odyssey BreadcrumbBar | 1–2 days | Medium |
| Desktop | Replace WpfApplicationFramework | 1–2 weeks | Medium |
| Desktop | Clean up project references | Half day | Low |
| Desktop | Visual polish | Optional | Low |

**Total realistic effort**: 3–4 weeks for everything, working incrementally. The Fluent theme can be applied immediately for an instant visual upgrade.

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Fluent theme conflicts with custom styles | Medium | Low | Test incrementally; override specific styles in `Generic.xaml` |
| CommunityToolkit.Mvvm API differences | Low | Medium | APIs are similar; migrate one ViewModel at a time |
| Razor Pages routing conflicts with existing Kestrel routes | Low | Low | Razor Pages can coexist with minimal API endpoints; use explicit route patterns |
| Removing `UseWPF` from non-UI projects breaks ObservableCollection usage | Medium | Low | Add explicit package references as needed |

## Success Criteria

1. Web interface renders correctly with Razor Pages, all file browsing and download functionality preserved.
2. Desktop client displays with Windows 11 Fluent theme on supported systems.
3. Odyssey dependency removed; BreadcrumbBar replaced with an equivalent control.
4. WpfApplicationFramework dependency removed; all ViewModels use CommunityToolkit.Mvvm.
5. Non-UI projects no longer reference WPF or WinForms SDKs.
6. No regression in existing integration tests.
