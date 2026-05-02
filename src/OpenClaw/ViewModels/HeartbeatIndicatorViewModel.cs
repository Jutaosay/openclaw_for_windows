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
            if (ReferenceEquals(_fillBrush, value))
            {
                return;
            }

            _fillBrush = value;
            PropertyChanged?.Invoke(this, EventArgsCache.FillBrush);
        }
    }

    public double FillOpacity
    {
        get => _fillOpacity;
        set
        {
            if (Math.Abs(_fillOpacity - value) < double.Epsilon)
            {
                return;
            }

            _fillOpacity = value;
            PropertyChanged?.Invoke(this, EventArgsCache.FillOpacity);
        }
    }

    private static class EventArgsCache
    {
        public static readonly PropertyChangedEventArgs FillBrush = new(nameof(FillBrush));
        public static readonly PropertyChangedEventArgs FillOpacity = new(nameof(FillOpacity));
    }
}
