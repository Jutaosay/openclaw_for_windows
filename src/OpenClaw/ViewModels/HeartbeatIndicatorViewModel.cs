// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OpenClaw.ViewModels;

public sealed class HeartbeatIndicatorViewModel : INotifyPropertyChanged
{
    private Brush _fillBrush = new SolidColorBrush(Color.FromArgb(255, 107, 114, 128));
    private double _fillOpacity = 0.32;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Brush FillBrush
    {
        get => _fillBrush;
        set
        {
            _fillBrush = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FillBrush)));
        }
    }

    public double FillOpacity
    {
        get => _fillOpacity;
        set
        {
            _fillOpacity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FillOpacity)));
        }
    }
}
