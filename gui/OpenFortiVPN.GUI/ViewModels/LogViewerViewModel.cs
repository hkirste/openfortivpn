using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;

namespace OpenFortiVPN.GUI.ViewModels;

/// <summary>
/// ViewModel for the log viewer — searchable, filterable, color-coded log display.
/// </summary>
public partial class LogViewerViewModel : ObservableObject
{
    private readonly IVpnService _vpnService;
    private readonly ISettingsService _settingsService;
    private readonly IDispatcherService _dispatcher;
    private readonly List<LogEntry> _allLogs = new();

    public ObservableCollection<LogEntry> FilteredLogs { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showDebug = true;

    [ObservableProperty]
    private bool _showInfo = true;

    [ObservableProperty]
    private bool _showWarnings = true;

    [ObservableProperty]
    private bool _showErrors = true;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private int _totalEntries;

    [ObservableProperty]
    private int _visibleEntries;

    public LogViewerViewModel(IVpnService vpnService,
                             ISettingsService settingsService,
                             IDispatcherService dispatcher)
    {
        _vpnService = vpnService;
        _settingsService = settingsService;
        _dispatcher = dispatcher;

        _vpnService.LogReceived += OnLogReceived;

        // Load buffered entries from before this VM was created
        foreach (var entry in _vpnService.LogBuffer)
            AddEntry(entry);
    }

    private void OnLogReceived(object? sender, LogEntry entry)
    {
        _dispatcher.Invoke(() => AddEntry(entry));
    }

    private void AddEntry(LogEntry entry)
    {
        _allLogs.Add(entry);

        var max = _settingsService.Current.MaxLogLines;
        while (_allLogs.Count > max)
            _allLogs.RemoveAt(0);

        TotalEntries = _allLogs.Count;

        if (ShouldShowEntry(entry))
        {
            FilteredLogs.Add(entry);
            VisibleEntries = FilteredLogs.Count;
        }
    }

    private bool ShouldShowEntry(LogEntry entry)
    {
        // Severity filter
        var severityMatch = entry.Severity switch
        {
            LogSeverity.Debug => ShowDebug,
            LogSeverity.Info => ShowInfo,
            LogSeverity.Warning => ShowWarnings,
            LogSeverity.Error => ShowErrors,
            _ => true
        };

        if (!severityMatch) return false;

        // Text filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            return entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnShowDebugChanged(bool value) => ApplyFilters();
    partial void OnShowInfoChanged(bool value) => ApplyFilters();
    partial void OnShowWarningsChanged(bool value) => ApplyFilters();
    partial void OnShowErrorsChanged(bool value) => ApplyFilters();

    private void ApplyFilters()
    {
        FilteredLogs.Clear();
        foreach (var entry in _allLogs)
        {
            if (ShouldShowEntry(entry))
                FilteredLogs.Add(entry);
        }

        VisibleEntries = FilteredLogs.Count;
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _allLogs.Clear();
        FilteredLogs.Clear();
        TotalEntries = 0;
        VisibleEntries = 0;
    }

    [RelayCommand]
    private void ExportLogs()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export Logs",
            FileName = $"openfortivpn-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            var lines = _allLogs.Select(e =>
                $"[{e.Timestamp:HH:mm:ss.fff}] [{e.Severity,-7}] {e.Message}");
            File.WriteAllLines(dlg.FileName, lines);
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        var text = string.Join(Environment.NewLine,
            FilteredLogs.Select(e =>
                $"[{e.Timestamp:HH:mm:ss.fff}] [{e.Severity,-7}] {e.Message}"));
        Clipboard.SetText(text);
    }
}
