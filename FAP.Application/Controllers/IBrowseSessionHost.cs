using FAP.Domain.Entities;

namespace FAP.Application.Controllers
{
    /// <summary>
    /// Hosts peer browse sessions in the desktop shell (WinUI Browse page).
    /// When registered, View Shares prefers this over popup windows.
    /// </summary>
    public interface IBrowseSessionHost
    {
        void OpenPeer(Node peer);
    }
}
