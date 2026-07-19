using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Services
{
    public interface IListenerServiceFactory
    {
        ListenerService Create(bool isServer);
    }

    public sealed class ListenerServiceFactory : IListenerServiceFactory
    {
        private readonly IServiceProvider _services;

        public ListenerServiceFactory(IServiceProvider services)
        {
            _services = services;
        }

        public ListenerService Create(bool isServer)
        {
            return new ListenerService(
                _services,
                isServer,
                _services.GetRequiredService<ILogger<ListenerService>>());
        }
    }
}
