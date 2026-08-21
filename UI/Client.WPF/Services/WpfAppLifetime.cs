using System.Windows;
using Fap.Foundation.Hosting;

namespace Fap.Presentation.Services
{
    public sealed class WpfAppLifetime : IAppLifetime
    {
        public void Shutdown(int exitCode = 0)
        {
            Application.Current?.Shutdown(exitCode);
        }
    }
}
