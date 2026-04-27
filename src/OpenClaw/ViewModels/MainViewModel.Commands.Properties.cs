// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    public System.Windows.Input.ICommand OpenSettingsCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand ReloadCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand StopCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand RetryCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand DevToolsCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand RunDiagnosticsCommand { get; private set; } = null!;
    public System.Windows.Input.ICommand ViewLogsCommand { get; private set; } = null!;
}
