namespace Fap.Foundation.Hosting
{
    public sealed class NoOpAppLifetime : IAppLifetime
    {
        public void Shutdown(int exitCode = 0) { }
    }
}