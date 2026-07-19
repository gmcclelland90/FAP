# FAP UI plan — WinUI desktop + guest web share

Living dual-track plan. WPF FluentTheme work is **superseded**; do not invest in App.xaml theme polyfills as the product UI.

## Locked decisions

- **Guest web share stays** — thin LAN HTTP browse/download for users without the client.
- **Guest web modernizes separately** — Razor in `FAP.GuestWeb` + `Web.Resources` assets. No shared XAML with desktop.
- **Desktop shell is WinUI 3** — `UI/Client.WinUI` (Fluent / Mica / NavigationView).
- **WPF** (`UI/Client.WPF`) is legacy / non-shipping.

## Track A — Guest web

| Item | Status |
|---|---|
| Modern HTML/CSS/JS (no jQuery) | Done |
| Light/dark via `prefers-color-scheme` | Done |
| `fap://` download icons | Preserved |
| Compiled Razor (`FAP.GuestWeb`) + `MapRazorPages` | Done |
| `BrowsePageRenderer` string twin removed | Done |

## Track B — Foundation (pre-WinUI)

| Item | Status |
|---|---|
| `IUiDispatcher` / `IAppLifetime` | Done |
| Peel Domain `System.Windows` avatar load | Done |
| `AddFapCore` / `AddFapClient` | Done |
| `IListenerServiceFactory` | Done |
| `IFapHttpClientFactory` + `IPeerOrchestration` + `ICompareSpecProvider` | Done |

## Track C — WinUI client

| Item | Status |
|---|---|
| Unpackaged `UI/Client.WinUI` scaffold | Done |
| Shared DI + `ApplicationCore` bootstrap | Done |
| Fluent shell (Mica, NavigationView, TabView) | Done |
| Feature pages bound to controllers/VMs | Done |
| Tray + second-instance `fap://` forward | Done |
| Shipping client in `build.ps1` / README | Done |
| Delete `Client.WPF` from repo | Optional follow-on |

## Build

```powershell
dotnet build UI/Client.WinUI/Client.WinUI.csproj -c Debug -p:Platform=x64
dotnet build FAP.GuestWeb/FAP.GuestWeb.csproj -c Debug
```
