using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using Fap.Foundation;
using Xunit;

namespace FAP.UnitTests;

public class SharesViewModelTests
{
    [Fact]
    public void Shares_collection_binds()
    {
        var vm = new SharesViewModel(new StubView());
        var observed = new SafeObservedCollection<Share>();
        observed.Add(new Share { Name = "Tmp", Path = "C:\\tmp" });
        var shares = new SafeObservingCollection<Share>(observed);
        vm.Shares = shares;

        Assert.Single(vm.Shares);
        Assert.Equal("Tmp", vm.Shares[0].Name);
    }
}