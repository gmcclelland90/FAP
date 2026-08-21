using FAP.Application.Controllers;
using FAP.Domain.Entities;
using Fap.Client.WinUI.Pages;

namespace Fap.Client.WinUI.Services;

public sealed class BrowseSessionHost : IBrowseSessionHost
{
    private readonly MainWindow _window;

    public BrowseSessionHost(MainWindow window)
    {
        _window = window;
    }

    public void OpenPeer(Node peer)
    {
        var page = _window.GetOrCreateBrowsePage();
        _window.NavigateToBrowse();
        page.OpenPeer(peer);
    }
}
