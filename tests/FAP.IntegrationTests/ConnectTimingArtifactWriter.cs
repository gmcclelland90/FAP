using System;
using System.IO;
using System.Text.Json;
using FAP.Shared.ConnectTiming;

namespace FAP.IntegrationTests;

internal static class ConnectTimingArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ArtifactsDir
    {
        get
        {
            var root = FindRepoRoot() ?? Directory.GetCurrentDirectory();
            return Path.Combine(root, "artifacts", "connect-timing");
        }
    }

    public static string Write(ConnectTimingReport report)
    {
        Directory.CreateDirectory(ArtifactsDir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var scenario = Sanitize(report.Scenario);
        var runtime = Sanitize(report.Runtime);
        var stamped = Path.Combine(ArtifactsDir, $"{runtime}-{scenario}-{stamp}.json");
        var latest = Path.Combine(ArtifactsDir, $"latest-{runtime}-{scenario}.json");
        var json = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(stamped, json);
        File.WriteAllText(latest, json);
        System.Console.WriteLine($"[ConnectTiming] wrote {latest} totalMs={report.TotalMs} budgetMs={report.BudgetMs}");
        return latest;
    }

    private static string Sanitize(string s) =>
        string.Join("-", (s ?? "unknown").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Fap.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
