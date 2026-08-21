using FAP.Domain.Verbs;

namespace FAP.IntegrationTests
{
    // Minimal stub for tests; we don't need UI chat handling here
    public sealed class TestConversationController : IConversationController
    {
        public bool HandleMessage(string id, string nickname, string message) => true;
    }
}


