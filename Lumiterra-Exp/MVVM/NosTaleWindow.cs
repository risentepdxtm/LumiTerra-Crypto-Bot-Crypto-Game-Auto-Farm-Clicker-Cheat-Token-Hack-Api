using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NosGame.MiniGames;
using Timer = System.Timers.Timer;

namespace NosGame.MVVM;

public sealed class NosTaleWindow : INotifyPropertyChanged
{
    public ViewModel ViewModel { get; set; }
    public IntPtr Handle { get; }
    public string Title { get; set; }

    private int _currentRepeat = 1;
    public int CurrentRepeat
    {
        get => _currentRepeat;
        set
        {
            SetProperty(ref _currentRepeat, value);
            OnPropertyChanged();
        }
    }
    private int _repeats = 20;
    public int Repeats
    {
        get => _repeats;
        set
        {
            SetProperty(ref _repeats, value);
            OnPropertyChanged();
        }
    }
    private bool _humanize;
    public bool Humanize
    {
        get => _humanize;
        set
        {
            SetProperty(ref _humanize, value);
            OnPropertyChanged();
        }
    }

    private bool _running;
    public bool Running
    {
        get => _running;
        set
        {
            SetProperty(ref _running, value);
            OnPropertyChanged();
        }
    }

    private int _level = 5;
    public int Level
    {
        get => _level;
        set
        {
            SetProperty(ref _level, value);
            OnPropertyChanged();
        }
    }
    
    private Games _game = 0;

    public Games Game
    {
        get => _game;
        set
        {
            SetProperty(ref _game, value);
            OnPropertyChanged();
        }
    }

    public NosTaleWindow(IntPtr handle, string title, ViewModel viewModel)
    {
        Handle = handle;
        Title = title;
        ViewModel = viewModel;
    }
    
    public Timer? GameTimer;

    public void StartBot()
    {
        GameTimer = new Timer(ViewModel.UpdateInterval == 0 ? 1 : ViewModel.UpdateInterval);
        
        GameTimer.Elapsed += delegate
        {
            GC.Collect();
        };

        switch (_game)
        {
            case Games.Fishpond:
            {
                var fishpond = new Fishpond(this);
                GameTimer.Elapsed += delegate
                {
                    fishpond.Update();
                };
                GameTimer.Disposed += delegate
                {
                    fishpond.StopReadPoints();
                };
                break;
            }
            case Games.Sawmill:
            {
                var sawmill = new Sawmill(this);
                GameTimer.Elapsed += delegate
                {
                    sawmill.Update();
                };
                GameTimer.Disposed += delegate
                {
                    sawmill.StopReadPoints();
                };
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        GameTimer.Enabled = true;
        GameTimer.Start();
    }

    public void StopBot()
    {
        if (GameTimer is not {Enabled: true}) return;
        
        GameTimer.Enabled = false;
        GameTimer.Stop();
        GameTimer.Dispose();
        _currentRepeat = 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;

        storage = value;
        OnPropertyChanged(propertyName);
    }
}