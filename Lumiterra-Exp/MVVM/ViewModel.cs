using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using static NosGame.Utils.NativeImports;

namespace NosGame.MVVM;

public sealed class ViewModel : INotifyPropertyChanged
{
    public ICommand ButtonStart {get; }

    private bool CanExecuteStart
    {
        get
        {
            return !Windows.Any(x => x.GameTimer is {Enabled: true});
        }
    }
    
    public ICommand ButtonStop { get; }

    private bool CanExecuteStop
    {
        get
        {
            return Windows.Any(x => x.GameTimer is {Enabled: true});
        }
    }

    public ICommand ButtonRefreshWindows
    {
        get;
    }

    private ObservableCollection<NosTaleWindow> _windows = new();
    public ObservableCollection<NosTaleWindow> Windows
    {
        get => _windows;
        set
        {
            SetField(ref _windows, value);
            OnPropertyChanged();
        }
    }

    private bool _isRunning = true;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            SetField(ref _isRunning, value);
            OnPropertyChanged();
        }
    }

    private int _updateInterval = Properties.Settings.Default.UpdateInterval;
    public int UpdateInterval
    {
        get => _updateInterval;
        set
        {
            SetField(ref _updateInterval, value);
            OnPropertyChanged();
            Properties.Settings.Default.UpdateInterval = value;
        }
    }

    private int _eventFishDelay = Properties.Settings.Default.EventFishDelay;
    public int EventFishDelay
    {
        get => _eventFishDelay;
        set
        {
            SetField(ref _eventFishDelay, value);
            OnPropertyChanged();
            Properties.Settings.Default.EventFishDelay = value;
        }
    }

    private int _pullUpDelay = Properties.Settings.Default.PullUpDelay;

    public int PullUpDelay
    {
        get => _pullUpDelay;
        set
        {
            SetField(ref _pullUpDelay, value);
            OnPropertyChanged();
            Properties.Settings.Default.PullUpDelay = value;
        }
    }
    

    public ViewModel()
    {
        ButtonRefreshWindows = new CommandHandler(RefreshWindows, () => CanExecuteStart);
        ButtonStop = new CommandHandler(StopBot, () => CanExecuteStop);
        ButtonStart = new CommandHandler(StartBot, () => CanExecuteStart);
        RefreshWindows();
    }

    private void StartBot()
    {
        foreach (var window in Windows)
        {
            if (!window.Running) continue;
            window.StartBot();
        }
        IsRunning = !Windows.Any(x => x.GameTimer is {Enabled: true});
    }

    private void StopBot()
    {
        foreach (var window in Windows)
        {
            if (window.GameTimer is {Enabled: false}) continue;
            window.StopBot();
        }
        
        IsRunning = !Windows.Any(x => x.GameTimer is {Enabled: true});
    }

    private void RefreshWindows()
    {
        this.Windows.Clear();
        EnumWindows(delegate(IntPtr wnd, IntPtr param)
        {
            var sb = new StringBuilder(256);
            GetWindowText(wnd, sb, sb.Capacity);
            if (!sb.ToString().Contains("NosTale")) return true;
            var newTitle = "NosTale - " + wnd;
            SetWindowText(wnd, newTitle);
            this.Windows.Add(new NosTaleWindow(wnd, newTitle, this));
            return true;

        }, IntPtr.Zero);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }
}