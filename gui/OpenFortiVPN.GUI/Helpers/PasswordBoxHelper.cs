using System.Windows;
using System.Windows.Controls;

namespace OpenFortiVPN.GUI.Helpers;

/// <summary>
/// Attached behavior that enables two-way binding for WPF PasswordBox.
/// Uses an Attach property to hook the PasswordChanged event, and a
/// BoundPassword property for the actual value.
/// </summary>
public static class PasswordBoxHelper
{
    // Attach=true hooks the PasswordChanged event
    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached(
            "Attach", typeof(bool), typeof(PasswordBoxHelper),
            new PropertyMetadata(false, OnAttachChanged));

    // The bound password value (two-way)
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword", typeof(string), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    public static bool GetAttach(DependencyObject d) =>
        (bool)d.GetValue(AttachProperty);

    public static void SetAttach(DependencyObject d, bool value) =>
        d.SetValue(AttachProperty, value);

    public static string GetBoundPassword(DependencyObject d) =>
        (string)d.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject d, string value) =>
        d.SetValue(BoundPasswordProperty, value);

    private static bool _updating;

    private static void OnAttachChanged(DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox pb) return;

        if ((bool)e.NewValue)
            pb.PasswordChanged += OnPasswordChanged;
        else
            pb.PasswordChanged -= OnPasswordChanged;
    }

    private static void OnBoundPasswordChanged(DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox pb) return;
        if (_updating) return;

        pb.Password = (string)e.NewValue ?? string.Empty;
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
