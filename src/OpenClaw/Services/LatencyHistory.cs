// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.Services;

public sealed class LatencyHistory
{
    private readonly int _capacity;
    private readonly Queue<long> _samples = new();

    public LatencyHistory(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _capacity = capacity;
    }

    public void Record(ControlUiLatencySnapshot snapshot)
    {
        if (!snapshot.IsSuccess || snapshot.RoundtripTimeMs is not long roundtripTimeMs)
        {
            return;
        }

        _samples.Enqueue(roundtripTimeMs);
        while (_samples.Count > _capacity)
        {
            _samples.Dequeue();
        }
    }

    public LatencyHistorySummary CreateSummary()
    {
        if (_samples.Count == 0)
        {
            return LatencyHistorySummary.Empty;
        }

        var values = _samples.ToArray();
        Array.Sort(values);

        var latest = _samples.Last();
        var average = (long)Math.Round(_samples.Average(), MidpointRounding.AwayFromZero);
        var p95Index = Math.Clamp((int)Math.Ceiling(values.Length * 0.95d) - 1, 0, values.Length - 1);

        return new LatencyHistorySummary(
            _samples.Count,
            latest,
            values[0],
            average,
            values[p95Index],
            values[^1]);
    }
}

public readonly record struct LatencyHistorySummary(
    int SampleCount,
    long? LatestMs,
    long? MinMs,
    long? AverageMs,
    long? P95Ms,
    long? MaxMs)
{
    public static LatencyHistorySummary Empty => new(0, null, null, null, null, null);
}

public static class LatencyTooltipFormatter
{
    public static string Format(LatencyHistorySummary summary)
    {
        if (summary.SampleCount <= 0 ||
            summary.LatestMs is not long latest ||
            summary.MinMs is not long min ||
            summary.AverageMs is not long average ||
            summary.P95Ms is not long p95 ||
            summary.MaxMs is not long max)
        {
            return "Latency history: no samples yet";
        }

        return string.Join(
            '\n',
            $"Latency history ({summary.SampleCount} samples)",
            $"Latest: {latest} ms",
            $"Min: {min} ms",
            $"Avg: {average} ms",
            $"P95: {p95} ms",
            $"Max: {max} ms");
    }
}
