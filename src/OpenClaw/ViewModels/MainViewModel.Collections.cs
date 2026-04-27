// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    public System.Collections.ObjectModel.ObservableCollection<EnvironmentConfig> Environments { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<HeartbeatIndicatorViewModel> HeartbeatIndicators { get; } =
        CreateIndicatorCollection(HeartbeatIndicatorCount, CreateHeartbeatIndicator);
    public System.Collections.ObjectModel.ObservableCollection<HeartbeatIndicatorViewModel> RunIndicators { get; } =
        CreateIndicatorCollection(RunIndicatorCount, CreateRunIndicator);
}
