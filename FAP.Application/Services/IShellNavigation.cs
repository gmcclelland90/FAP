namespace FAP.Application.Services;

public interface IShellNavigation
{
    void GoBack();
    void NavigateTag(string tag);
    /// <summary>Open the Chat page; optionally select/create a peer DM thread.</summary>
    void NavigateToChat(string? peerId = null);
}
