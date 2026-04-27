// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Models;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    public EnvironmentConfig? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (_selectedEnvironment != value)
            {
                UpdateEnvironmentSelection(value, persistSelection: true);
            }
        }
    }

    public string CurrentUrl => _selectedEnvironment?.GatewayUrl ?? string.Empty;

    public string SelectedEnvironmentName => _selectedEnvironment?.Name ?? string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ConnectionState ConnectionState
    {
        get => _connectionState;
        private set => SetProperty(ref _connectionState, value);
    }

    public Brush StatusIndicatorBrush
    {
        get => _statusIndicatorBrush;
        private set => SetProperty(ref _statusIndicatorBrush, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoadingVisibility));
        }
    }

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        private set => SetProperty(ref _isErrorVisible, value);
    }

    public bool ShowRetryButton
    {
        get => _showRetryButton;
        private set
        {
            if (SetProperty(ref _showRetryButton, value))
            {
                OnPropertyChanged(nameof(RetryButtonVisibility));
            }
        }
    }

    public Visibility RetryButtonVisibility => ShowRetryButton ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets the underlying WebViewService for binding to the WebView2 control.
    /// </summary>
    public WebViewService WebViewService => _webViewService;

    /// <summary>
    /// Gets the hosted UI bridge.
    /// </summary>
    public HostedUiBridge HostedUiBridge => _hostedUiBridge;

    /// <summary>
    /// Gets the shell session coordinator.
    /// </summary>
    public ShellSessionCoordinator? Coordinator => _coordinator;
}
