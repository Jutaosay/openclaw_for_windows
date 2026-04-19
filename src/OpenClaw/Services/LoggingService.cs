// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Globalization;
using System.Text.Json;

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

    public void Info(string message) => Write("INFO", message, null);
    public void Warning(string message) => Write("WARN", message, null);
    public void Error(string message) => Write("ERROR", message, null);

    /// <summary>
    /// Writes a structured log entry with optional context data.
    /// </summary>
    public void Info(string eventKey, object? context = null) => Write("INFO", eventKey, context);
    public void Warning(string eventKey, object? context = null) => Write("WARN", eventKey, context);
    public void Error(string eventKey, object? context = null) => Write("ERROR", eventKey, context);

    private void Write(string level, string message, object? context)
    {
        lock (_lock)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                var logEntry = new StructuredLogEntry
                {
                    Timestamp = timestamp,
                    Level = level,
                    Message = message,
                    Context = context
                };

                var line = JsonSerializer.Serialize(logEntry);
                File.AppendAllText(CurrentLogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Swallow logging failures to avoid cascading errors
            }
        }
    }

    /// <summary>
    /// Writes a simple log entry (backward compatibility).
    /// </summary>
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

/// <summary>
/// Represents a structured log entry.
/// </summary>
public record StructuredLogEntry
{
    public string Timestamp { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public object? Context { get; init; }
}
