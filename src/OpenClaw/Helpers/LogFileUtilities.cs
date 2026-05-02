// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.Helpers;

public static class LogFileUtilities
{
    public const int DefaultTailLineCount = 500;

    public static LogTailResult ReadLastLines(string path, int maxLines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLines);

        var tail = new Queue<string>(maxLines);
        var totalLineCount = 0;

        foreach (var line in File.ReadLines(path))
        {
            totalLineCount++;

            if (maxLines == 0)
            {
                continue;
            }

            if (tail.Count == maxLines)
            {
                tail.Dequeue();
            }

            tail.Enqueue(line);
        }

        return new LogTailResult(tail.ToArray(), totalLineCount);
    }

    public static int DeleteExpiredLogs(string logDirectory, TimeSpan retention, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(logDirectory) || !Directory.Exists(logDirectory))
        {
            return 0;
        }

        var cutoffUtc = now.Subtract(retention).UtcDateTime;
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(logDirectory, "openclaw-*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) >= cutoffUtc)
                {
                    continue;
                }

                File.Delete(file);
                deleted++;
            }
            catch
            {
                // Retention is best-effort; logging should never fail startup.
            }
        }

        return deleted;
    }
}

public readonly record struct LogTailResult(IReadOnlyList<string> Lines, int TotalLineCount)
{
    public bool WasTruncated => Lines.Count < TotalLineCount;
}
