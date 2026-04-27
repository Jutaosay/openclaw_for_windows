// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    private static System.Collections.ObjectModel.ObservableCollection<HeartbeatIndicatorViewModel> CreateIndicatorCollection(
        int count,
        Func<int, HeartbeatIndicatorViewModel> factory)
    {
        var indicators = new System.Collections.ObjectModel.ObservableCollection<HeartbeatIndicatorViewModel>();
        for (var index = 0; index < count; index++)
        {
            indicators.Add(factory(index));
        }

        return indicators;
    }
}
