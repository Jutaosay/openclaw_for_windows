namespace OpenClaw.Services;

public static class AppTelemetry
{
    public static Func<int>? DeferredSaveRequestsProvider { get; set; }

    public static Func<int>? DeferredSaveCoalescedRequestsProvider { get; set; }

    public static int DeferredSaveRequests => DeferredSaveRequestsProvider?.Invoke() ?? 0;

    public static int DeferredSaveCoalescedRequests => DeferredSaveCoalescedRequestsProvider?.Invoke() ?? 0;
}
