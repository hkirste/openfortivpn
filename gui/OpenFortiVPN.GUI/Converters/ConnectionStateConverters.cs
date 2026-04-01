using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Converters;

/// <summary>
/// Maps ConnectionState to a color brush for the status indicator.
/// </summary>
public class ConnectionStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ConnectionState state) return Brushes.Gray;

        return state switch
        {
            ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(0x0F, 0x7B, 0x0F)),
            ConnectionState.Connecting or
            ConnectionState.Authenticating or
            ConnectionState.NegotiatingTunnel => new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
            ConnectionState.Reconnecting => new SolidColorBrush(Color.FromRgb(0xCA, 0x50, 0x10)),
            ConnectionState.Error => new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C)),
            ConnectionState.WaitingForOtp or
            ConnectionState.WaitingForSaml => new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00)),
            _ => new SolidColorBrush(Color.FromRgb(0x8A, 0x88, 0x86)),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps ConnectionState to a status icon glyph (Segoe Fluent Icons).
/// </summary>
public class ConnectionStateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ConnectionState state) return "\uE839"; // StatusCircle

        return state switch
        {
            ConnectionState.Connected => "\uE930",       // Lock (secured)
            ConnectionState.Connecting => "\uE895",      // Sync
            ConnectionState.Authenticating => "\uE8D7",  // Permissions
            ConnectionState.NegotiatingTunnel => "\uE895",
            ConnectionState.Reconnecting => "\uE72C",    // Refresh
            ConnectionState.Error => "\uEA39",           // StatusErrorFull
            ConnectionState.WaitingForOtp => "\uE8D7",
            ConnectionState.WaitingForSaml => "\uE774",  // Globe
            ConnectionState.Disconnecting => "\uE895",
            _ => "\uE871",                               // Shield (disconnected)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a boolean to Visibility (true = Visible, false = Collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var invert = parameter is "Invert";
        return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>
/// Converts LogSeverity to a colored brush for log viewer.
/// </summary>
public class LogSeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LogSeverity severity) return Brushes.Black;

        return severity switch
        {
            LogSeverity.Error => new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C)),
            LogSeverity.Warning => new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00)),
            LogSeverity.Debug => new SolidColorBrush(Color.FromRgb(0x8A, 0x88, 0x86)),
            _ => new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts enum values to display-friendly strings.
/// </summary>
public class EnumToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TlsVersion tls) return tls.ToDisplayString();
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
