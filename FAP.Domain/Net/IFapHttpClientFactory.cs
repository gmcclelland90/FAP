using System.Net.Http;
using FAP.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Net
{
    public interface IFapHttpClientFactory
    {
        ModernHttpClient Create(Node localNode);
        Client CreateLegacy(Node localNode);
    }

    public sealed class FapHttpClientFactory : IFapHttpClientFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ModernHttpClient> _logger;

        public FapHttpClientFactory(IHttpClientFactory httpClientFactory, ILogger<ModernHttpClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public ModernHttpClient Create(Node localNode)
        {
            return new ModernHttpClient(localNode, _logger, _httpClientFactory.CreateClient("FapDefault"));
        }

        public Client CreateLegacy(Node localNode)
        {
            return new Client(localNode);
        }
    }
}
