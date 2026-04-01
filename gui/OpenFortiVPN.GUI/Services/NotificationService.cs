using System.Windows;
using Microsoft.Extensions.Logging;
using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

public sealed class NotificationService : INotificationService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ISettingsService settings, ILogger<NotificationService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void ShowInfo(string title, string message)
    {
        if (!_settings.Current.ShowNotifications) return;
        ShowTrayNotification(title, message, NotificationType.Info);
    }

    public void ShowWarning(string title, string message)
    {
        if (!_settings.Current.ShowNotifications) return;
        ShowTrayNotification(title, message, NotificationType.Warning);
    }

    public void ShowError(string title, string message)
    {
        // Errors are always shown regardless of notification setting
        ShowTrayNotification(title, message, NotificationType.Error);
    }

    public void ShowConnectionEstablished(string profileName, string assignedIp)
    {
        ShowInfo("VPN Connected", $"Connected to {profileName}\nIP: {assignedIp}");
    }

    public void ShowConnectionLost(string profileName, string? reason)
    {
        ShowWarning("VPN Disconnected",
            string.IsNullOrEmpty(reason)
                ? $"Disconnected from {profileName}"
                : $"Disconnected from {profileName}: {reason}");
    }

    public void ShowReconnecting(string profileName, int attempt)
    {
        ShowInfo("VPN Reconnecting", $"Reconnecting to {profileName} (attempt {attempt})...");
    }

    private enum NotificationType { Info, Warning, Error }

    private void ShowTrayNotification(string title, string message, NotificationType type)
    {
        _logger.LogDebug("Notification [{Type}]: {Title} - {Message}", type, title, message);

        // The actual tray icon balloon is triggered via the MainViewModel
        // which owns the NotifyIcon. We raise an event that the VM subscribes to.
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            NotificationRequested?.Invoke(this, new NotificationEventArgs(title, message, type));
        });
    }

    public event EventHandler<NotificationEventArgs>? NotificationRequested;

    public record NotificationEventArgs(string Title, string Message, NotificationType Type);
}
