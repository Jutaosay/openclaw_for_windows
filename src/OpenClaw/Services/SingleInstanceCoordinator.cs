// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.IO.Pipes;
using System.Text;

namespace OpenClaw.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string ActivationCommand = "activate";
    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;
    private readonly string _pipeName;
    private readonly IAppLogger _logger;
    private CancellationTokenSource? _listenCancellation;
    private Task? _listenTask;
    private bool _isDisposed;

    private SingleInstanceCoordinator(Mutex mutex, bool ownsMutex, string pipeName, IAppLogger logger)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
        _pipeName = pipeName;
        _logger = logger;
    }

    public const string DefaultMutexName = @"Local\OpenClaw.Manager.SingleInstance";

    public const string DefaultPipeName = "OpenClaw.Manager.SingleInstance";

    public event Action? ActivationRequested;

    public bool IsPrimary => _ownsMutex;

    public static SingleInstanceCoordinator CreatePrimaryOrSecondary(IAppLogger logger) =>
        CreatePrimaryOrSecondary(DefaultMutexName, DefaultPipeName, logger);

    public static SingleInstanceCoordinator CreatePrimaryOrSecondary(string mutexName, string pipeName, IAppLogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(logger);

        var mutex = new Mutex(initiallyOwned: false, mutexName);
        var ownsMutex = TryOwnMutex(mutex);
        return new SingleInstanceCoordinator(mutex, ownsMutex, pipeName, logger);
    }

    public static bool RequestActivationOfPrimaryInstance(IAppLogger logger) =>
        RequestActivationOfPrimaryInstance(DefaultPipeName, logger);

    public static bool RequestActivationOfPrimaryInstance(string pipeName, IAppLogger logger, int timeoutMilliseconds = 750)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipe.Connect(timeoutMilliseconds);
            using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(ActivationCommand);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            logger.Warning($"Failed to request primary instance activation: {ex.Message}");
            return false;
        }
    }

    public void StartListening()
    {
        if (_isDisposed || !IsPrimary || _listenTask is not null)
        {
            return;
        }

        _listenCancellation = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenForActivationRequestsAsync(_listenCancellation.Token));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _listenCancellation?.Cancel();

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
        }

        _listenCancellation?.Dispose();
        _mutex.Dispose();
    }

    private async Task ListenForActivationRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8);
                var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.Equals(command, ActivationCommand, StringComparison.Ordinal))
                {
                    ActivationRequested?.Invoke();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.Warning($"Single-instance activation listener failed: {ex.Message}");
            }
        }
    }

    private static bool TryOwnMutex(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}
