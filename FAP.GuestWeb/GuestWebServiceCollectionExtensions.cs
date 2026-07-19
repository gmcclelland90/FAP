using System.Diagnostics;
using FAP.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FAP.GuestWeb
{
    public static class GuestWebServiceCollectionExtensions
    {
        /// <summary>
        /// Registers compiled Razor browse rendering for any host (WinUI, WPF, console, Kestrel).
        /// </summary>
        public static IServiceCollection AddFapGuestWeb(this IServiceCollection services)
        {
            // Generic hosts (WinUI/WPF) lack the ASP.NET Core DiagnosticSource registration
            // that RazorPageActivator requires.
            services.TryAddSingleton<DiagnosticListener>(_ => new DiagnosticListener("FAP.GuestWeb"));
            services.TryAddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());

            services.AddLogging();
            services.AddOptions();
            services.AddRazorPages();
            services.AddControllersWithViews()
                .AddApplicationPart(typeof(GuestWebMarker).Assembly);
            services.TryAddSingleton<IBrowsePageHtmlRenderer, RazorBrowsePageHtmlRenderer>();
            return services;
        }
    }
}
