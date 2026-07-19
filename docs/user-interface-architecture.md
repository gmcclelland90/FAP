# User Interface Architecture

## Overview

FAP has two UI surfaces:

1. **Desktop client** — WinUI 3 / Windows App SDK (`UI/Client.WinUI`), Fluent shell (Mica, NavigationView, TabView popups).
2. **Guest web share** — thin LAN HTML from compiled Razor (`FAP.GuestWeb`), served by Kestrel for browse/download without the client.

The legacy WPF project (`UI/Client.WPF`) remains in the solution for reference until full retirement; it is **not** the shipping client.

## Desktop MVVM stack

```
┌─────────────────────────────────────┐
│   View (WinUI Pages / UserControls) │
├─────────────────────────────────────┤
│   ViewModel (FAP.Application)       │
├─────────────────────────────────────┤
│   Controller (FAP.Application)      │
├─────────────────────────────────────┤
│   Domain / Network                  │
└─────────────────────────────────────┘
```

Composition uses `Microsoft.Extensions.DependencyInjection`:

- `AddFapCore()` — Domain, Network, guest Razor renderer, HTTP factories
- `AddFapClient()` — controllers and ViewModels
- Host-specific adapters: `IUiDispatcher`, `IAppLifetime`, `IMessageService`, `IQuery`, `ITrayIconView`, feature views

## WinUI shell

| Area | Implementation |
|---|---|
| Main window | Mica + `NavigationView` + `TitleBar` |
| Feature pages | Browse, Search, Queue, Shares, Compare, Settings |
| Home | Peers, chat input, transfer sessions |
| Popups | `TabView` host via `IPopupWindowController` |
| Dialogs | `ContentDialog` / `IMessageService` |
| Help | `WebView2` (`IWebPanel`) |
| Tray | `Shell_NotifyIcon` + context menu → `TrayIconViewModel` |
| Protocol | `-url` / `fap://` on launch; second instance RPC via `ADDDOWNLOAD` |

## Guest web share

- Markup: `FAP.GuestWeb/Views/Guest/Browse.cshtml` (compiled Razor)
- Renderer: `IBrowsePageHtmlRenderer` / `RazorBrowsePageHtmlRenderer`
- Assets: `FAP.Domain/Web.Resources` (`fap.css`, `browse.js`)
- Host: `ModernNodeServer` — `AddFapGuestWeb()`, `MapRazorPages()`, catch-all handler for listings/files
- URLs: `/`, `/{share}/…`, `/Fap.app.web/…`, smoke `/Fap.api/guest-ui`

## Seams (UI-free Domain)

- `IUiDispatcher` — replaces WPF `Dispatcher` in Foundation collections
- `IAppLifetime` — replaces `Application.Current.Shutdown`
- Dialogs and folder pickers behind `IMessageService` / `IQuery`

## Related docs

- [`docs/upgrade-plan-ui.md`](upgrade-plan-ui.md) — dual-track living plan
- [`CONTEXT.md`](../CONTEXT.md) — product glossary
- [`docs/http-integration.md`](http-integration.md) — guest HTTP details
