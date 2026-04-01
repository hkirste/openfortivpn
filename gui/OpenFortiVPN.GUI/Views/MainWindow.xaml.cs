using System.ComponentModel;
using System.Windows;
using OpenFortiVPN.GUI.Services;
using OpenFortiVPN.GUI.ViewModels;

namespace OpenFortiVPN.GUI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;

    public MainWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = viewModel;

        // Restore window position
        var settings = _settingsService.Current;
        if (!double.IsNaN(settings.WindowLeft))
        {
            Left = settings.WindowLeft;
            Top = settings.WindowTop;
        }

        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Save window position
        var settings = _settingsService.Current;
        settings.WindowLeft = Left;
        settings.WindowTop = Top;
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
        _ = _settingsService.SaveAsync();

        // Minimize to tray instead of closing
        if (settings.CloseToTray)
        {
            e.Cancel = true;
            _viewModel.HideWindowCommand.Execute(null);
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settingsService.Current.MinimizeToTray)
        {
            _viewModel.HideWindowCommand.Execute(null);
            WindowState = WindowState.Normal;
        }

        base.OnStateChanged(e);
    }
}
