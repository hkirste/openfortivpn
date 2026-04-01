using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Controls;

public partial class StatusIndicator : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State),
            typeof(ConnectionState),
            typeof(StatusIndicator),
            new PropertyMetadata(ConnectionState.Disconnected, OnStateChanged));

    public ConnectionState State
    {
        get => (ConnectionState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public StatusIndicator()
    {
        InitializeComponent();
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusIndicator indicator)
        {
            var newState = (ConnectionState)e.NewValue;
            var pulse = (Storyboard)indicator.Resources["PulseAnimation"];

            var isAnimating = newState is ConnectionState.Connecting
                or ConnectionState.Authenticating
                or ConnectionState.NegotiatingTunnel
                or ConnectionState.Reconnecting
                or ConnectionState.Disconnecting;

            if (isAnimating)
                pulse.Begin();
            else
                pulse.Stop();
        }
    }
}
