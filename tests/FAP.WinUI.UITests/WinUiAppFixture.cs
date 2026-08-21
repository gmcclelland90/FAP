using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace FAP.WinUI.UITests;

/// <summary>
/// Launches the unpackaged WinUI client for FlaUI smokes. Set FAP_WINUI_EXE to override the path.
/// </summary>
public sealed class WinUiAppFixture : IDisposable
{
    public Application? App { get; private set; }
    public UIA3Automation Automation { get; } = new();
    public string ExePath { get; }

    public WinUiAppFixture()
    {
        ExePath = ResolveExePath();
    }

    public Window LaunchAndWaitForMain(TimeSpan? timeout = null)
    {
        if (!File.Exists(ExePath))
            throw new FileNotFoundException(
                $"WinUI exe not found at '{ExePath}'. Build UI/Client.WinUI (x64 Debug) or set FAP_WINUI_EXE.",
                ExePath);

        // Avoid colliding with a developer-owned instance when possible.
        foreach (var p in Process.GetProcessesByName("Fap.Client.WinUI"))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        App = Application.Launch(ExePath);
        var wait = timeout ?? TimeSpan.FromSeconds(45);
        var deadline = DateTime.UtcNow + wait;
        while (DateTime.UtcNow < deadline)
        {
            var win = App.GetMainWindow(Automation, TimeSpan.FromSeconds(2));
            if (win != null && !string.IsNullOrWhiteSpace(win.Title))
            {
                win.SetForeground();
                return win;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException("Timed out waiting for WinUI main window.");
    }

    public void Dispose()
    {
        try { App?.Close(); } catch { /* ignore */ }
        try { App?.Dispose(); } catch { /* ignore */ }
        Automation.Dispose();
        foreach (var p in Process.GetProcessesByName("Fap.Client.WinUI"))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }
    }

    private static string ResolveExePath()
    {
        var env = Environment.GetEnvironmentVariable("FAP_WINUI_EXE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var root = FindRepoRoot();
        var candidates = new[]
        {
            Path.Combine(root, "UI", "Client.WinUI", "bin", "x64", "Debug", "net9.0-windows10.0.26100.0", "win-x64", "Fap.Client.WinUI.exe"),
            Path.Combine(root, "UI", "Client.WinUI", "bin", "Debug", "net9.0-windows10.0.26100.0", "win-x64", "Fap.Client.WinUI.exe"),
            Path.Combine(root, "artifacts", "publish", "client", "win-x64", "Debug", "Fap.Client.WinUI.exe"),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        return candidates[0];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Fap.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}