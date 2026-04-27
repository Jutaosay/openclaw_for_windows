// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Collections.Concurrent;
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

    private readonly object _fileLock = new();
    private readonly ConcurrentQueue<string> _pendingLines = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly CancellationTokenSource _writerCts = new();
    private readonly Task _writerTask;
    private int _disposeState;

    public LoggingService()
    {
        Directory.CreateDirectory(LogFolder);
        _writerTask = Task.Run(ProcessQueueAsync);
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
        if (Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

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

            EnqueueLine(JsonSerializer.Serialize(logEntry));
        }
        catch
        {
            // Swallow logging failures to avoid cascading errors
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _writerCts.Cancel();

        try
        {
            _queueSignal.Release();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort flush during shutdown.
        }

        FlushPendingLines();
        _writerCts.Dispose();
        _queueSignal.Dispose();
    }

    private void EnqueueLine(string line)
    {
        _pendingLines.Enqueue(line);

        try
        {
            _queueSignal.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (true)
            {
                await _queueSignal.WaitAsync(_writerCts.Token).ConfigureAwait(false);
                FlushPendingLines();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            FlushPendingLines();
        }
    }

    private void FlushPendingLines()
    {
        if (_pendingLines.IsEmpty)
        {
            return;
        }

        var batch = new List<string>(16);
        while (_pendingLines.TryDequeue(out var line))
        {
            batch.Add(line);

            if (batch.Count >= 32)
            {
                WriteBatch(batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            WriteBatch(batch);
        }
    }

    private void WriteBatch(List<string> lines)
    {
        lock (_fileLock)
        {
            try
            {
                File.AppendAllText(CurrentLogFilePath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
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
