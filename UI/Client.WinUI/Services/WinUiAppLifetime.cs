using Fap.Foundation.Hosting;
using Microsoft.UI.Xaml;

namespace Fap.Client.WinUI.Services;

public sealed class WinUiAppLifetime : IAppLifetime
{
    public void Shutdown(int exitCode = 0)
    {
        Application.Current?.Exit();
        Environment.ExitCode = exitCode;
    }
}
