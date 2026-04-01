namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Displays system tray notifications and toast messages.
/// </summary>
public interface INotificationService
{
    void ShowInfo(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    void ShowConnectionEstablished(string profileName, string assignedIp);
    void ShowConnectionLost(string profileName, string? reason);
    void ShowReconnecting(string profileName, int attempt);
}
