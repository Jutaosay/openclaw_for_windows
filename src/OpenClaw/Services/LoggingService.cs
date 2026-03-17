// Copyright (c) OpenClaw. All rights reserved.

using System.Globalization;

namespace OpenClaw.Services;

/// <summary>
/// Provides structured local logging to JSON-lines files.
/// Log files are stored in %LOCALAPPDATA%\OpenClaw\logs\ and rotated daily.
/// </summary>
public class LoggingService
{
    private static readonly string LogFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw", "logs");

    private readonly object _lock = new();

    public LoggingService()
    {
        Directory.CreateDirectory(LogFolder);
    }

    /// <summary>
    /// Gets the path to the current log file.
    /// </summary>
    public string CurrentLogFilePath =>
        Path.Combine(LogFolder, $"openclaw-{DateTime.UtcNow:yyyy-MM-dd}.log");

    /// <summary>
    /// Gets the path to the log folder.
    /// </summary>
    public string LogFolderPath => LogFolder;

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        lock (_lock)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                var line = $"[{timestamp}] [{level}] {message}";
                File.AppendAllText(CurrentLogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Swallow logging failures to avoid cascading errors
            }
        }
    }
}
