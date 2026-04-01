using CommunityToolkit.Mvvm.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class NavigationServiceTests
{
    private sealed partial class ViewModelA : ObservableObject { }
    private sealed partial class ViewModelB : ObservableObject { }

    private sealed partial class ViewModelC : ObservableObject, IParameterReceiver
    {
        public object? ReceivedParam { get; private set; }
        public void ReceiveParameter(object parameter) => ReceivedParam = parameter;
    }

    private static NavigationService CreateService()
    {
        var services = new ServiceCollection();
        services.AddTransient<ViewModelA>();
        services.AddTransient<ViewModelB>();
        services.AddTransient<ViewModelC>();
        return new NavigationService(services.BuildServiceProvider());
    }

    [Fact]
    public void NavigateTo_SetsCurrentViewModel()
    {
        var nav = CreateService();
        nav.NavigateTo<ViewModelA>();
        nav.CurrentViewModel.Should().BeOfType<ViewModelA>();
    }

    [Fact]
    public void NavigateTo_FiresNavigatedEvent()
    {
        var nav = CreateService();
        ObservableObject? received = null;
        nav.Navigated += (_, vm) => received = vm;

        nav.NavigateTo<ViewModelA>();

        received.Should().BeOfType<ViewModelA>();
    }

    [Fact]
    public void NavigateTo_PushesHistory()
    {
        var nav = CreateService();
        nav.NavigateTo<ViewModelA>();
        nav.CanGoBack.Should().BeFalse();

        nav.NavigateTo<ViewModelB>();
        nav.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public void GoBack_RestoresPreviousViewModel()
    {
        var nav = CreateService();
        nav.NavigateTo<ViewModelA>();
        var firstVm = nav.CurrentViewModel;

        nav.NavigateTo<ViewModelB>();
        nav.CurrentViewModel.Should().BeOfType<ViewModelB>();

        nav.GoBack();
        nav.CurrentViewModel.Should().BeSameAs(firstVm);
    }

    [Fact]
    public void GoBack_EmptyHistory_DoesNothing()
    {
        var nav = CreateService();
        nav.NavigateTo<ViewModelA>();
        var current = nav.CurrentViewModel;

        nav.GoBack();
        nav.CurrentViewModel.Should().BeSameAs(current);
    }

    [Fact]
    public void NavigateTo_WithParameter_PassesToReceiver()
    {
        var nav = CreateService();
        nav.NavigateTo<ViewModelA>(); // Need a first VM for history
        nav.NavigateTo<ViewModelC>("test-param");

        var vm = nav.CurrentViewModel as ViewModelC;
        vm.Should().NotBeNull();
        vm!.ReceivedParam.Should().Be("test-param");
    }

    [Fact]
    public void NavigateTo_MultipleSteps_CanGoBackAll()
    {
        var nav = CreateService();
        nav.NavigateTo<ViewModelA>();
        nav.NavigateTo<ViewModelB>();
        nav.NavigateTo<ViewModelC>();

        nav.CanGoBack.Should().BeTrue();
        nav.GoBack();
        nav.CurrentViewModel.Should().BeOfType<ViewModelB>();
        nav.GoBack();
        nav.CurrentViewModel.Should().BeOfType<ViewModelA>();
        nav.CanGoBack.Should().BeFalse();
    }
}
