using System.Diagnostics;
using FAP.Shared.Models;
using FAP.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace FAP.GuestWeb
{
    public sealed class RazorBrowsePageHtmlRenderer : IBrowsePageHtmlRenderer
    {
        private const string ViewPath = "/Views/Guest/Browse.cshtml";
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;

        public RazorBrowsePageHtmlRenderer(
            IRazorViewEngine viewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
        }

        public async Task<string> RenderAsync(BrowsePageData model, CancellationToken cancellationToken = default)
        {
            var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath: ViewPath, isMainPage: true);
            if (!viewResult.Success)
                viewResult = _viewEngine.FindView(actionContext, "Guest/Browse", isMainPage: true);

            if (!viewResult.Success)
            {
                var searched = string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>());
                throw new InvalidOperationException($"Guest browse Razor view not found. Searched: {searched}");
            }

            await using var writer = new StringWriter();
            var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };
            var tempData = new TempDataDictionary(httpContext, _tempDataProvider);
            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                tempData,
                writer,
                new HtmlHelperOptions());

            await viewResult.View.RenderAsync(viewContext);
            Debug.Assert(writer.GetStringBuilder().Length > 0);
            return writer.ToString();
        }
    }
}
