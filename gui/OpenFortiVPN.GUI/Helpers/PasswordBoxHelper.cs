using System.Windows;
using System.Windows.Controls;

namespace OpenFortiVPN.GUI.Helpers;

/// <summary>
/// Attached behavior that enables binding for WPF PasswordBox.
/// WPF intentionally prevents PasswordBox.Password from being a
/// DependencyProperty for security. This helper bridges the gap
/// for MVVM binding while keeping passwords out of the visual tree.
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    public static string GetBoundPassword(DependencyObject d) =>
        (string)d.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject d, string value) =>
        d.SetValue(BoundPasswordProperty, value);

    private static bool _updating;

    private static void OnBoundPasswordChanged(DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox pb) return;
        if (_updating) return;

        pb.PasswordChanged -= OnPasswordChanged;
        pb.Password = (string)e.NewValue;
        pb.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender,
        RoutedEventArgs e)
    {
        if (sender is not PasswordBox pb) return;

        _updating = true;
        SetBoundPassword(pb, pb.Password);
        _updating = false;
    }
}
