using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace OpenFortiVPN.GUI.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<ObservableObject> _history = new();
    private ObservableObject _currentViewModel = null!;

    public ObservableObject CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            _currentViewModel = value;
            Navigated?.Invoke(this, value);
        }
    }

    public event EventHandler<ObservableObject>? Navigated;
    public bool CanGoBack => _history.Count > 0;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
    {
        if (_currentViewModel is not null)
            _history.Push(_currentViewModel);

        CurrentViewModel = _serviceProvider.GetRequiredService<TViewModel>();
    }

    public void NavigateTo<TViewModel>(object parameter) where TViewModel : ObservableObject
    {
        if (_currentViewModel is not null)
            _history.Push(_currentViewModel);

        var vm = _serviceProvider.GetRequiredService<TViewModel>();

        // If the ViewModel accepts parameters, pass them
        if (vm is IParameterReceiver receiver)
            receiver.ReceiveParameter(parameter);

        CurrentViewModel = vm;
    }

    public void GoBack()
    {
        if (_history.Count > 0)
            CurrentViewModel = _history.Pop();
    }
}

/// <summary>
/// Implemented by ViewModels that accept navigation parameters.
/// </summary>
public interface IParameterReceiver
{
    void ReceiveParameter(object parameter);
}
