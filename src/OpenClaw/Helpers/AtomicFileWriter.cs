// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.Helpers;

public static class AtomicFileWriter
{
    public static void WriteAllText(string path, string contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        var backupPath = $"{fullPath}.{Guid.NewGuid():N}.bak";
        var backupCreated = false;

        try
        {
            File.WriteAllText(tempPath, contents);

            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, backupPath, ignoreMetadataErrors: true);
                backupCreated = true;
                return;
            }

            File.Move(tempPath, fullPath);
        }
        finally
        {
            TryDelete(tempPath);
            if (backupCreated)
            {
                TryDelete(backupPath);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; the caller should see the original write failure.
        }
    }
}
