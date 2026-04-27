// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Windows.Input;

namespace OpenClaw.Helpers;

/// <summary>
/// A simple <see cref="ICommand"/> implementation for binding.
/// </summary>
public class SimpleCommand : ICommand
{
    private readonly Action _action;

    public SimpleCommand(Action action) => _action = action;

#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _action();
}

/// <summary>
/// A simple async <see cref="ICommand"/> implementation that safely observes failures.
/// </summary>
public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _action;
    private readonly Action<Exception>? _errorHandler;

    public AsyncCommand(Func<Task> action, Action<Exception>? errorHandler = null)
    {
        _action = action;
        _errorHandler = errorHandler;
    }

#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        var task = _action();
        Observe(task);
    }

    private async void Observe(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _errorHandler?.Invoke(ex);
        }
    }
}
