namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Abstracts the WPF dispatcher for testability. Production implementation
/// uses Application.Current.Dispatcher; tests use synchronous execution.
/// </summary>
public interface IDispatcherService
{
    void Invoke(Action action);
}

/// <summary>
/// WPF dispatcher implementation for production use.
/// </summary>
public sealed class WpfDispatcherService : IDispatcherService
{
    public void Invoke(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}

/// <summary>
/// Synchronous dispatcher for unit tests — runs actions immediately.
/// </summary>
public sealed class SynchronousDispatcherService : IDispatcherService
{
    public void Invoke(Action action) => action();
}
