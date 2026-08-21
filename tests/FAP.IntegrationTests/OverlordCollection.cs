using Xunit;

namespace FAP.IntegrationTests
{
    [CollectionDefinition("Overlord", DisableParallelization = true)]
    public class OverlordCollection : ICollectionFixture<OverlordHostFixture>
    {
        // No code—just a collection fixture marker to share OverlordHostFixture
    }
}


