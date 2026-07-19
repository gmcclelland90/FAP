using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace FAP.GuestWeb.UITests;

public class GuestBrowseUiTests : IClassFixture<GuestHostFixture>, IAsyncLifetime
{
    private readonly GuestHostFixture _fx;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public GuestBrowseUiTests(GuestHostFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task Root_shows_breadcrumb_and_file_table()
    {
        var page = await NewPageAsync();
        await page.GotoAsync(_fx.BaseUrl + "/");
        await Expect(page.Locator("nav.breadcrumb")).ToBeVisibleAsync();
        await Expect(page.Locator("#files")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Tmp" })).ToBeVisibleAsync();
        await Expect(page.Locator("#guest-q")).ToBeVisibleAsync();
        System.Console.WriteLine("[Playwright] root structure ok");
    }

    [Fact]
    public async Task Open_in_FAP_is_opt_in()
    {
        var page = await NewPageAsync();
        await page.GotoAsync(_fx.BaseUrl + "/");
        await page.GetByRole(AriaRole.Link, new() { Name = "Tmp" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Expect(page.Locator("text=hello.txt")).ToBeVisibleAsync();

        // Hidden by default (opt-in off)
        await Expect(page.Locator("a.fap-link").First).ToBeHiddenAsync();
        Assert.Equal(0, await page.Locator("a.fap-link").EvaluateAllAsync<int>("els => els.filter(e => e.offsetParent !== null).length"));

        await page.Locator("#fap-optin").CheckAsync();
        var fap = page.Locator("a.fap-link").First;
        await Expect(fap).ToBeVisibleAsync();
        await Expect(fap).ToContainTextAsync("Open in FAP");
        var href = await fap.GetAttributeAsync("href");
        Assert.StartsWith("fap://", href ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        System.Console.WriteLine("[Playwright] fap opt-in ok");
    }

    [Fact]
    public async Task Search_finds_file_beyond_current_listing()
    {
        var page = await NewPageAsync();
        // Root listing has only Tmp folder — hello.txt is inside Tmp
        await page.GotoAsync(_fx.BaseUrl + "/?q=hello");
        await Expect(page.Locator("#files")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "hello.txt" })).ToBeVisibleAsync();
        await Expect(page.Locator(".search-meta")).ToContainTextAsync("match");
        System.Console.WriteLine("[Playwright] search ok");
    }

    [Fact]
    public async Task Assets_and_sort_headers_present()
    {
        var page = await NewPageAsync();
        await page.GotoAsync(_fx.BaseUrl + "/");
        var css = await page.EvaluateAsync<bool>("() => !!document.querySelector('link[href*=\"fap.css\"]')");
        var js = await page.EvaluateAsync<bool>("() => !!document.querySelector('script[src*=\"browse.js\"]')");
        Assert.True(css);
        Assert.True(js);
        var applied = await page.EvaluateAsync<bool>("""
            () => {
              const bg = getComputedStyle(document.body).backgroundColor;
              const font = getComputedStyle(document.body).fontFamily;
              return bg !== 'rgba(0, 0, 0, 0)' && !font.includes('Times New Roman');
            }
            """);
        Assert.True(applied, "fap.css linked but styles not applied (check file encoding is UTF-8)");
        await Expect(page.Locator("th[data-sort='name'] button.sort-btn")).ToBeVisibleAsync();
        await page.ClickAsync("th[data-sort='name'] button.sort-btn");
        var ariaSort = await page.Locator("th[data-sort='name']").GetAttributeAsync("aria-sort");
        Assert.False(string.IsNullOrEmpty(ariaSort));
        await Expect(page.Locator("nav.breadcrumb a[aria-current='page']")).ToBeVisibleAsync();
        System.Console.WriteLine("[Playwright] assets + sort ok");
    }

    [Fact]
    public async Task Root_screenshot_baseline()
    {
        var page = await NewPageAsync();
        await page.SetViewportSizeAsync(1280, 720);
        await page.GotoAsync(_fx.BaseUrl + "/");
        await page.WaitForSelectorAsync("#files");

        var dir = Path.Combine("screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "guest-root.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
        System.Console.WriteLine($"[Playwright] screenshot {path}");
    }

    /// <summary>
    /// Rich agent-feedback pack: root / search / opt-in / narrow + HTML/a11y while the fixture is up.
    /// Invoked by scripts/capture-guest-feedback.ps1 when FAP_CAPTURE_DIR is set.
    /// </summary>
    [Fact]
    public async Task Capture_agent_feedback_pack()
    {
        var captureDir = Environment.GetEnvironmentVariable("FAP_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(captureDir))
        {
            // Keep CI green when not capturing — still smoke the key surfaces.
            captureDir = Path.Combine("screenshots", "agent-feedback-smoke");
        }

        Directory.CreateDirectory(captureDir);
        var page = await NewPageAsync();

        // 1) Root (chips off)
        await page.SetViewportSizeAsync(1280, 720);
        await page.GotoAsync(_fx.BaseUrl + "/");
        await page.WaitForSelectorAsync("#files");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(captureDir, "page.png"),
            FullPage = true
        });
        await File.WriteAllTextAsync(Path.Combine(captureDir, "page.html"), await page.ContentAsync());
        await WriteA11yHintsAsync(page, Path.Combine(captureDir, "a11y.json"));

        // 2) Host search (beyond current listing)
        await page.GotoAsync(_fx.BaseUrl + "/?q=hello");
        await page.WaitForSelectorAsync("#files");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(captureDir, "search.png"),
            FullPage = true
        });
        await File.WriteAllTextAsync(Path.Combine(captureDir, "search.html"), await page.ContentAsync());

        // 3) Opt-in chips visible
        await page.GotoAsync(_fx.BaseUrl + "/");
        await page.GetByRole(AriaRole.Link, new() { Name = "Tmp" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.Locator("#fap-optin").CheckAsync();
        await Expect(page.Locator("a.fap-link").First).ToBeVisibleAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(captureDir, "optin-on.png"),
            FullPage = true
        });

        // 4) Narrow viewport — FAP opt-in/chips hidden (no phone client)
        await page.SetViewportSizeAsync(390, 844);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(captureDir, "narrow-optin.png"),
            FullPage = true
        });

        // 5) Empty folder
        await page.SetViewportSizeAsync(1280, 720);
        await page.GotoAsync(_fx.BaseUrl + "/Tmp/empty/");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(captureDir, "empty.png"),
            FullPage = true
        });

        // 6) Unresolved path
        await page.GotoAsync(_fx.BaseUrl + "/NoSuchPath/");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(captureDir, "path-error.png"),
            FullPage = true
        });

        // 7) Busy slots (MaxUploads=0 after defaults → FreeUploadSlots=0)
        var prevMax = _fx.Model.MaxUploads;
        _fx.Model.MaxUploads = 0;
        try
        {
            await page.GotoAsync(_fx.BaseUrl + "/");
            await page.WaitForSelectorAsync("#files");
            await Expect(page.Locator(".slot-state.red")).ToContainTextAsync("Busy");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(captureDir, "busy.png"),
                FullPage = true
            });
        }
        finally
        {
            _fx.Model.MaxUploads = prevMax;
        }

        // 8) Dark color scheme
        var darkPage = await NewPageAsync(ColorScheme.Dark);
        await darkPage.SetViewportSizeAsync(1280, 720);
        await darkPage.GotoAsync(_fx.BaseUrl + "/");
        await darkPage.WaitForSelectorAsync("#files");
        await darkPage.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(captureDir, "dark.png"),
            FullPage = true
        });
        await darkPage.Context.CloseAsync();

        System.Console.WriteLine($"[Playwright] agent feedback pack {captureDir}");
    }

    private async Task<IPage> NewPageAsync(ColorScheme colorScheme = ColorScheme.Light)
    {
        Assert.NotNull(_browser);
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ColorScheme = colorScheme,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });
        return await context.NewPageAsync();
    }

    private static async Task WriteA11yHintsAsync(IPage page, string path)
    {
        var hints = await page.EvaluateAsync<object>("""
            () => {
              const q = (s) => !!document.querySelector(s);
              return {
                title: document.title,
                hasBreadcrumb: q('nav.breadcrumb'),
                breadcrumbCurrent: !!document.querySelector('nav.breadcrumb [aria-current="page"]'),
                hasFilesTable: q('#files'),
                hasSearch: q('#guest-q'),
                hasFapOptIn: q('#fap-optin'),
                showFapLinks: document.documentElement.classList.contains('show-fap-links'),
                fapLinkCount: document.querySelectorAll('a.fap-link').length,
                fapLinkVisibleCount: [...document.querySelectorAll('a.fap-link')].filter(e => e.offsetParent !== null).length,
                sortButtons: document.querySelectorAll('button.sort-btn').length,
                hasFapCss: !!document.querySelector('link[href*="fap.css"]'),
                hasBrowseJs: !!document.querySelector('script[src*="browse.js"]'),
                bodyFont: getComputedStyle(document.body).fontFamily,
                bodyBg: getComputedStyle(document.body).backgroundColor
              };
            }
            """);
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(hints, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
