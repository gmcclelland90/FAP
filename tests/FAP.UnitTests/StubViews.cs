using FAP.Application.Views;

namespace FAP.UnitTests;

internal sealed class StubView :
    IMainWindow,
    IDownloadQueue,
    ISharesView,
    ISearchView,
    ISettingsView,
    ICompareView,
    IBrowserView
{
    public object DataContext { get; set; } = null!;
    public void Show() { }
    public void Close() { }
    public void Flash() { }
}