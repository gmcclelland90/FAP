# Legacy WPF client

This project is **not** the shipping FAP client. The product UI is `UI/Client.WinUI`.

Kept temporarily for reference and emergency fallback builds:

```powershell
dotnet build UI/Client.WPF/Fap.Presentation.csproj -c Debug
```

Do not add new features here. Prefer WinUI ports and shared Application/Domain changes.
