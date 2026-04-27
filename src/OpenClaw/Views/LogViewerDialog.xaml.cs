// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Controls;
using OpenClaw.Helpers;

namespace OpenClaw.Views;

/// <summary>
/// Dialog for viewing application log files.
/// </summary>
public sealed partial class LogViewerDialog : ContentDialog
{
    private readonly string _logDirectory;

    public LogViewerDialog()
    {
        this.InitializeComponent();
        _logDirectory = App.Logger.LogFolderPath;
        LoadTodayLog();
    }

    private void LoadTodayLog()
    {
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var logFile = Path.Combine(_logDirectory, $"openclaw-{today}.log");
            LogFileLabel.Text = string.Format(StringResources.LogFileLabelFormat, $"openclaw-{today}.log");

            if (File.Exists(logFile))
            {
                var content = File.ReadAllText(logFile);
                // Show last 500 lines max
                var lines = content.Split('\n');
                if (lines.Length > 500)
                {
                    LogContent.Text = string.Format(StringResources.LogShowingLastLinesFormat, lines.Length) + "\n\n"
                        + string.Join('\n', lines.Skip(lines.Length - 500));
                }
                else
                {
                    LogContent.Text = content;
                }
            }
            else
            {
                LogContent.Text = StringResources.LogNotFoundToday;
            }
        }
        catch (Exception ex)
        {
            LogContent.Text = string.Format(StringResources.LogReadFailedFormat, ex.Message);
        }
    }

    private void OnRefresh(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        LoadTodayLog();
    }

    private void OnOpenLogFolder(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(_logDirectory))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_logDirectory)
                {
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to open log folder: {ex.Message}");
        }
    }
}
