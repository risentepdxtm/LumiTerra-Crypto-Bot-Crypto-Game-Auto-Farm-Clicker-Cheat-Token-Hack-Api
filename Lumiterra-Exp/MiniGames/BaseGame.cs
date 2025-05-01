using System;
using System.Timers;
using NosGame.MVVM;

namespace NosGame.MiniGames;

public abstract class BaseGame
{
    public abstract NosTaleWindow Window { get; set; }
    public abstract int Points { get; set; }
    protected abstract Timer? ReadPoints { get; set; }
    public abstract void Update();

    public void StopReadPoints()
    {
        if (ReadPoints is not {Enabled: true}) return;
        
        ReadPoints.Enabled = false;
        ReadPoints.Stop();
        ReadPoints.Dispose();
        Points = 0;
    }
}