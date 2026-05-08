// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.Helpers;

internal static class WindowBoundsUtilities
{
    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 800;
    public const int MinimumPersistedWindowWidth = 640;
    public const int MinimumPersistedWindowHeight = 480;

    private const int HiddenWindowCoordinateThreshold = -30000;
    private const int MinimumVisibleWidth = 96;
    private const int MinimumVisibleHeight = 64;

    public static bool HasPersistableSize(double width, double height) =>
        IsFinite(width) &&
        IsFinite(height) &&
        width >= MinimumPersistedWindowWidth &&
        height >= MinimumPersistedWindowHeight;

    public static bool HasMinimizedSentinelPosition(double left, double top) =>
        IsFinite(left) &&
        IsFinite(top) &&
        (left <= HiddenWindowCoordinateThreshold || top <= HiddenWindowCoordinateThreshold);

    public static bool HasSavedPosition(double left, double top) =>
        IsFinite(left) &&
        IsFinite(top) &&
        left != -1 &&
        top != -1 &&
        !HasMinimizedSentinelPosition(left, top);

    public static bool CanPersistWindowBounds(int left, int top, int width, int height) =>
        HasPersistableSize(width, height) &&
        !HasMinimizedSentinelPosition(left, top);

    public static bool IsVisibleWithinAnyWorkArea(
        int left,
        int top,
        int width,
        int height,
        IEnumerable<WindowWorkArea> workAreas)
    {
        foreach (var area in workAreas)
        {
            if (IsVisibleWithinWorkArea(left, top, width, height, area))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryCenterInWorkArea(
        int width,
        int height,
        WindowWorkArea workArea,
        out int left,
        out int top)
    {
        left = 0;
        top = 0;

        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return false;
        }

        left = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
        top = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);
        return true;
    }

    private static bool IsVisibleWithinWorkArea(
        int left,
        int top,
        int width,
        int height,
        WindowWorkArea workArea)
    {
        var visibleWidth = GetIntersectionLength(left, width, workArea.Left, workArea.Width);
        var visibleHeight = GetIntersectionLength(top, height, workArea.Top, workArea.Height);

        return visibleWidth >= Math.Min(width, MinimumVisibleWidth) &&
               visibleHeight >= Math.Min(height, MinimumVisibleHeight);
    }

    private static int GetIntersectionLength(int firstStart, int firstLength, int secondStart, int secondLength)
    {
        var firstEnd = firstStart + firstLength;
        var secondEnd = secondStart + secondLength;
        return Math.Max(0, Math.Min(firstEnd, secondEnd) - Math.Max(firstStart, secondStart));
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}

internal readonly record struct WindowWorkArea(int Left, int Top, int Width, int Height);
