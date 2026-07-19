using FAP.Shared.Models;

namespace FAP.Shared.Services
{
    public interface IBrowsePageHtmlRenderer
    {
        Task<string> RenderAsync(BrowsePageData model, CancellationToken cancellationToken = default);
    }
}
