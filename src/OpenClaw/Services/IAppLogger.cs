// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.Services;

public interface IAppLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Info(string eventKey, object? context = null);
    void Warning(string eventKey, object? context = null);
    void Error(string eventKey, object? context = null);
}

internal sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    private NullAppLogger()
    {
    }

    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message) { }
    public void Info(string eventKey, object? context = null) { }
    public void Warning(string eventKey, object? context = null) { }
    public void Error(string eventKey, object? context = null) { }
}
