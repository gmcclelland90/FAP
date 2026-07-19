using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using Xunit;

namespace FAP.UnitTests;

public class MainWindowViewModelTests
{
    [Fact]
    public void Nickname_and_WindowTitle_raise_property_changed()
    {
        var vm = new MainWindowViewModel(new StubView());
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        vm.Nickname = "Alice";
        vm.WindowTitle = "FAP - Alice";

        Assert.Equal("Alice", vm.Nickname);
        Assert.Equal("FAP - Alice", vm.WindowTitle);
        Assert.Contains("Nickname", changed);
        Assert.Contains("WindowTitle", changed);
    }

    [Fact]
    public void Model_assignment_exposes_model()
    {
        var vm = new MainWindowViewModel(new StubView());
        var model = new Model();
        vm.Model = model;
        Assert.Same(model, vm.Model);
    }
}