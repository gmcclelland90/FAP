namespace FAP.Application.Services;

public interface IGettingStartedUi
{
    /// <summary>Show the Fluent getting-started dialog (blocking until dismissed on UI thread).</summary>
    void ShowGettingStarted();
}
