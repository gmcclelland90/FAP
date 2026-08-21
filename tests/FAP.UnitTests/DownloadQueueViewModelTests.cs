using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using Fap.Foundation;
using Xunit;

namespace FAP.UnitTests;

public class DownloadQueueViewModelTests
{
    [Fact]
    public void Stats_and_queue_bind()
    {
        var vm = new DownloadQueueViewModel(new StubView());
        var observed = new SafeObservedCollection<DownloadRequest>();
        observed.Add(new DownloadRequest());
        var queue = new SafeObservingCollection<DownloadRequest>(observed);

        vm.DownloadQueue = queue;
        vm.DownloadStats = "1 active";
        vm.UploadStats = "0 uploads";

        Assert.Single(vm.DownloadQueue);
        Assert.Equal("1 active", vm.DownloadStats);
        Assert.Equal("0 uploads", vm.UploadStats);
    }
}