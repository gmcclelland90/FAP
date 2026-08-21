using FAP.Shared.Entities;

namespace FAP.Shared.Interfaces
{
    public interface IVerb
    {
        NetworkRequest CreateRequest();
        bool ReceiveResponse(NetworkRequest response);
    }
} 