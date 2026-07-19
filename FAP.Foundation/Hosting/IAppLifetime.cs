namespace Fap.Foundation.Hosting
{
    public interface IAppLifetime
    {
        void Shutdown(int exitCode = 0);
    }
}
