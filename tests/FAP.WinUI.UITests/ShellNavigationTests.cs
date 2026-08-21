using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Tools;
using Xunit;

namespace FAP.WinUI.UITests;

[Trait("Category", "UI")]
public class ShellNavigationTests : IClassFixture<WinUiAppFixture>
{
    private readonly WinUiAppFixture _fx;

    public ShellNavigationTests(WinUiAppFixture fx) => _fx = fx;

    [Fact]
    public void Shell_starts_and_nav_landmarks_exist()
    {
        var window = _fx.LaunchAndWaitForMain();
        Assert.Contains("FAP", window.Title, StringComparison.OrdinalIgnoreCase);

        // Nav items may be exposed as ListItem/Button depending on WinUI UIA mapping.
        Assert.True(WaitForAutomationId(window, "nav.home", TimeSpan.FromSeconds(20)), "nav.home");
        Assert.True(WaitForAutomationId(window, "nav.browse", TimeSpan.FromSeconds(5)), "nav.browse");
        Assert.True(WaitForAutomationId(window, "nav.chat", TimeSpan.FromSeconds(5)), "nav.chat");
        Assert.True(WaitForAutomationId(window, "nav.search", TimeSpan.FromSeconds(5)), "nav.search");
        Assert.True(WaitForAutomationId(window, "nav.queue", TimeSpan.FromSeconds(5)), "nav.queue");
        Assert.True(WaitForAutomationId(window, "nav.shares", TimeSpan.FromSeconds(5)), "nav.shares");
        Assert.True(WaitForAutomationId(window, "nav.compare", TimeSpan.FromSeconds(5)), "nav.compare");
        Assert.True(WaitForAutomationId(window, "nav.settings", TimeSpan.FromSeconds(5)), "nav.settings");
        DumpCaptureArtifacts(window, "shell");
        System.Console.WriteLine("[FlaUI] shell nav landmarks ok");
    }

    [Fact]
    public void Navigate_search_and_queue_hosts_visible()
    {
        var window = _fx.LaunchAndWaitForMain();

        InvokeNav(window, "nav.home", "Home");
        Assert.True(
            WaitForAutomationId(window, "home.peers", TimeSpan.FromSeconds(20))
            || WaitForName(window, "Peers", TimeSpan.FromSeconds(5)),
            "home surface");
        DumpCaptureArtifacts(window, "home");

        InvokeNav(window, "nav.browse", "Browse");
        Assert.True(
            WaitForAutomationId(window, "browse.root", TimeSpan.FromSeconds(20))
            || WaitForAutomationId(window, "browse.empty", TimeSpan.FromSeconds(5))
            || WaitForName(window, "Browse peer shares", TimeSpan.FromSeconds(5)),
            "browse surface");
        DumpCaptureArtifacts(window, "browse");

        InvokeNav(window, "nav.chat", "Chat");
        Assert.True(
            WaitForAutomationId(window, "chat.threads", TimeSpan.FromSeconds(20))
            || WaitForAutomationId(window, "chat.messages", TimeSpan.FromSeconds(5))
            || WaitForAutomationId(window, "chat.composer", TimeSpan.FromSeconds(5))
            || WaitForName(window, "Chat", TimeSpan.FromSeconds(5)),
            "chat surface");
        DumpCaptureArtifacts(window, "chat");

        InvokeNav(window, "nav.search", "Search");
        Assert.True(
            WaitForAutomationId(window, "search.query", TimeSpan.FromSeconds(20))
            || WaitForAutomationId(window, "search.view", TimeSpan.FromSeconds(5))
            || WaitForName(window, "Search shares…", TimeSpan.FromSeconds(5)),
            "search surface");
        DumpCaptureArtifacts(window, "search");

        InvokeNav(window, "nav.queue", "Queue");
        Assert.True(
            WaitForAutomationId(window, "queue.list", TimeSpan.FromSeconds(20))
            || WaitForAutomationId(window, "queue.view", TimeSpan.FromSeconds(5))
            || WaitForName(window, "Remove all", TimeSpan.FromSeconds(5)),
            "queue surface");
        DumpCaptureArtifacts(window, "queue");

        InvokeNav(window, "nav.shares", "Shares");
        Assert.True(
            WaitForAutomationId(window, "shares.list", TimeSpan.FromSeconds(20))
            || WaitForAutomationId(window, "shares.view", TimeSpan.FromSeconds(5))
            || WaitForName(window, "Shares", TimeSpan.FromSeconds(5)),
            "shares surface");
        DumpCaptureArtifacts(window, "shares");

        InvokeNav(window, "nav.compare", "Compare");
        Assert.True(
            WaitForAutomationId(window, "compare.list", TimeSpan.FromSeconds(20))
            || WaitForAutomationId(window, "compare.view", TimeSpan.FromSeconds(5))
            || WaitForName(window, "Run compare", TimeSpan.FromSeconds(5)),
            "compare surface");
        DumpCaptureArtifacts(window, "compare");

        InvokeNav(window, "nav.settings", "Settings");
        Assert.True(
            WaitForAutomationId(window, "settings.nickname", TimeSpan.FromSeconds(20))
            || WaitForAutomationId(window, "settings.view", TimeSpan.FromSeconds(5))
            || WaitForName(window, "Nickname", TimeSpan.FromSeconds(5)),
            "settings surface");
        DumpCaptureArtifacts(window, "settings");

        System.Console.WriteLine("[FlaUI] feature surfaces navigation ok");
    }

    [Fact]
    public void Second_instance_with_url_exits_quickly()
    {
        var window = _fx.LaunchAndWaitForMain();
        Assert.NotNull(window);

        var psi = new ProcessStartInfo
        {
            FileName = _fx.ExePath,
            Arguments = "-url fap://testhost/Tmp/hello.txt",
            UseShellExecute = false
        };
        using var second = Process.Start(psi);
        Assert.NotNull(second);
        var exited = second!.WaitForExit(15000);
        Assert.True(exited, "Second instance should exit after forwarding ADDDOWNLOAD");
        System.Console.WriteLine($"[FlaUI] second instance exit code={second.ExitCode}");
    }

    private static bool WaitForAutomationId(AutomationElement root, string automationId, TimeSpan timeout)
    {
        var result = Retry.WhileNull(
            () => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout,
            TimeSpan.FromMilliseconds(200));
        return result.Success && result.Result != null;
    }

    private static bool WaitForName(AutomationElement root, string name, TimeSpan timeout)
    {
        var result = Retry.WhileNull(
            () => root.FindFirstDescendant(cf => cf.ByName(name)),
            timeout,
            TimeSpan.FromMilliseconds(200));
        return result.Success && result.Result != null;
    }

    private static void InvokeNav(AutomationElement root, string automationId, string nameFallback)
    {
        var el = Retry.WhileNull(
            () => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                  ?? root.FindFirstDescendant(cf => cf.ByName(nameFallback)),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(200)).Result;
        Assert.NotNull(el);
        try
        {
            if (el!.Patterns.Invoke.IsSupported)
                el.Patterns.Invoke.Pattern.Invoke();
            else if (el.Patterns.SelectionItem.IsSupported)
                el.Patterns.SelectionItem.Pattern.Select();
            else
                el.Click();
        }
        catch
        {
            el!.Click();
        }
        Thread.Sleep(700);
    }

    private static void DumpCaptureArtifacts(Window window, string label)
    {
        var dir = Environment.GetEnvironmentVariable("FAP_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        try
        {
            using var img = Capture.Element(window);
            img.ToFile(Path.Combine(dir, $"{label}.png"));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(dir, $"{label}-capture-error.txt"), ex.ToString());
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"title\": \"{Escape(window.Title)}\",");
        sb.AppendLine("  \"elements\": [");
        try
        {
            var all = window.FindAllDescendants();
            var first = true;
            foreach (var el in all)
            {
                string? id = null;
                string? name = null;
                try { id = el.Properties.AutomationId.ValueOrDefault; } catch { /* ignore */ }
                try { name = el.Properties.Name.ValueOrDefault; } catch { /* ignore */ }
                if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name)) continue;
                if (!first) sb.AppendLine(",");
                first = false;
                sb.Append($"    {{\"automationId\":\"{Escape(id)}\",\"name\":\"{Escape(name)}\"}}");
            }
            sb.AppendLine();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"    {{\"error\":\"{Escape(ex.Message)}\"}}");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        var json = sb.ToString();
        // Per-page dump for critique loop evidence; uia.json kept as last-page legacy alias.
        File.WriteAllText(Path.Combine(dir, $"uia-{label}.json"), json);
        File.WriteAllText(Path.Combine(dir, "uia.json"), json);
    }

    private static string Escape(string? s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
}