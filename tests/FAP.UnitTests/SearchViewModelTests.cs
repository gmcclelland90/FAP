using FAP.Application.ViewModel;
using FAP.Domain.Entities;
using Fap.Foundation;
using Xunit;

namespace FAP.UnitTests;

public class SearchViewModelTests
{
    [Fact]
    public void SearchString_and_AllowSearch_roundtrip()
    {
        var vm = new SearchViewModel(new StubView());
        vm.SearchString = "hello.txt";
        vm.AllowSearch = false;

        Assert.Equal("hello.txt", vm.SearchString);
        Assert.False(vm.AllowSearch);
    }

    [Fact]
    public void Results_collection_can_be_set()
    {
        var vm = new SearchViewModel(new StubView());
        var observed = new SafeObservedCollection<SearchResult>();
        observed.Add(new SearchResult { FileName = "hello.txt" });
        var results = new SafeObservingCollection<SearchResult>(observed);
        vm.Results = results;

        Assert.Single(vm.Results);
        Assert.Equal("hello.txt", vm.Results[0].FileName);
    }
}