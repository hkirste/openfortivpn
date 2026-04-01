using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Manages content-area navigation within the single main window.
/// </summary>
public interface INavigationService
{
    ObservableObject CurrentViewModel { get; }
    event EventHandler<ObservableObject>? Navigated;

    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
    void NavigateTo<TViewModel>(object parameter) where TViewModel : ObservableObject;
    bool CanGoBack { get; }
    void GoBack();
}
