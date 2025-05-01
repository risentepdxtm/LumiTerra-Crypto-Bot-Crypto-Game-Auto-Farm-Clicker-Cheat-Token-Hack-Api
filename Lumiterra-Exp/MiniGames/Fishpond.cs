using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using NosGame.MVVM;
using NosGame.Utils;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace NosGame.MiniGames;

public class Fishpond : BaseGame
{
    public sealed override NosTaleWindow Window { get; set; }
    public override int Points { get; set; }
    protected override Timer? ReadPoints { get; set; }

    private States _currentState = States.Idle;
    private CouponRefillStates _couponRefillState = CouponRefillStates.CloseGame;
    private int _usedCoupons;

    private int _waitTicks = 20;
    private HashSet<Rods> _lastRod = new HashSet<Rods>();
    private readonly Vec3b _fishColor = new(255, 247, 198);
    private readonly Vec3b _eventFishColor = new(46, 46, 46);
    private readonly Vec3b _vampireColor = new(43, 46, 213);

    private readonly int _delayAfterPulling;

    private enum Rods
    {
        None,
        Left,
        Right,
        Up,
        Down
    }

    private readonly int[] _levelPoints = {0, 1000, 4000, 8000, 12000, 20000};

    public Fishpond(NosTaleWindow window)
    {
        Window = window;
        _delayAfterPulling = 300 + Window.ViewModel.PullUpDelay;
    }

    public override void Update()
    {
        var screenshot = NativeImports.GetWindowScreenshot(Window.Handle);
        var ssMat = screenshot.ToMat();
        Console.WriteLine("Current state: " + _currentState);
        Console.WriteLine("Rods: " + string.Join(", ", _lastRod));

        switch (_currentState)
        {
            case States.Idle:
            {
                var ssMat2 = ssMat[new Rect(ssMat.Cols / 2 - 100, ssMat.Rows / 2 - 100, 200, 200)];
                var template = Images.OpenGame;
                var result = Images.FindTemplate(ssMat2, template);
                if (result.Value < 0.8) return;

                var loc = new Point(ssMat.Width / 2 - ssMat2.Width / 2 + result.Location.X + 10,
                    ssMat.Height / 2 - ssMat2.Height / 2 + result.Location.Y + 10);
                NativeImports.ClickAt(Window.Handle, loc);

                _currentState = States.StartingGame;
                break;
            }

            case States.StartingGame:
            {
                if (_waitTicks-- > 0) return;
                var ssMat2 =
                    ssMat[
                        new Rect(ssMat.Cols / 2 - 185, ssMat.Rows / 2 + 100, 185, 100)];
                var template = Images.StartingGame;
                var result = Images.FindTemplate(ssMat2, template);
                if (result.Value < 0.8) return;

                var loc = new Point(ssMat.Width / 2 - ssMat2.Width / 2 + result.Location.X + 30,
                    ssMat.Height / 2 + ssMat2.Height / 2 + result.Location.Y + 80);
                NativeImports.ClickAt(Window.Handle, loc);

                _currentState = States.StartGame;
                WaitTicks();
                break;
            }

            case States.StartGame:
            {
                if (ReadPoints != null)
                {
                    ReadPoints.Enabled = false;
                    ReadPoints?.Stop();
                    Points = 0;
                }

                if (_waitTicks-- > 0) return;

                var ssMat2 =
                    ssMat[
                        new Rect(ssMat.Cols / 2 - 128, ssMat.Rows / 2 + 150, 256, 100)];
                var template = Images.StartGame;
                var result = Images.FindTemplate(ssMat2, template);

                if (result.Value < 0.75) return;


                var loc = new Point(ssMat.Width / 2 - ssMat2.Width / 2 + result.Location.X + 10,
                    ssMat.Height / 2 + ssMat2.Height / 2 + result.Location.Y + 25);
                NativeImports.MoveTo(Window.Handle, loc);
                NativeImports.ClickAt(Window.Handle, loc);

                _currentState = States.Playing;
                break;
            }

            case States.Playing:
            {
                if (_waitTicks-- > 0) return;

                if (ReadPoints is not {Enabled: true})
                {
                    ReadPoints = new Timer(1000);
                    ReadPoints.Elapsed += delegate
                    {
                        var points = 0;
                        var mat = NativeImports.GetWindowScreenshot(Window.Handle).ToMat();
                        var ssMat2 =
                            mat[
                                new Rect(mat.Width / 2 - 181, mat.Height / 2 - 197,
                                    220, 26)];
                        Utils.ReadPoints.GetPoints(ssMat2, ref points);
                        if (points - 10 > Points)
                            Points = points;
                    };
                    ReadPoints.Enabled = true;
                    ReadPoints.Start();
                }

                if (Points > _levelPoints[Window.Level] + 10)
                {
                    _currentState = States.GettingReward;
                    WaitTicks();
                    break;
                }

                var resultMat =
                    ssMat[
                        new Rect(ssMat.Cols / 2 - 100, ssMat.Rows / 2 - 100, 200, 70)];

                var template = Images.Result;
                var result = Images.FindTemplate(resultMat, template);

                if (result.Value > 0.9)
                {
                    _currentState = States.Failed;
                    WaitTicks();
                    break;
                }

                var fishEvent = ssMat[
                    new Rect(ssMat.Cols / 2 - 230, ssMat.Rows / 2 - 10, 5, 5)];


                for (var x = 0; x < fishEvent.Width; x++)
                {
                    for (var y = 0; y < fishEvent.Height; y++)
                    {
                        var pixel = fishEvent.At<Vec3b>(y, x);
                        if (!Images.PixelTolerance(pixel, _eventFishColor, 10)) continue;
                        _currentState = States.QtEvent;
                        break;
                    }
                }

                var fishLeft = ssMat[new Rect(ssMat.Cols / 2 - 120, ssMat.Rows / 2 - 15, 5, 5)];
                var fishTop = ssMat[new Rect(ssMat.Cols / 2 + 36, ssMat.Rows / 2 - 63, 5, 5)];
                var fishBottom = ssMat[new Rect(ssMat.Cols / 2 - 3, ssMat.Rows / 2 + 44, 5, 5)];
                var fishRight = ssMat[new Rect(ssMat.Cols / 2 + 122, ssMat.Rows / 2 - 17, 5, 5)];

                var fishLeftVampireCheck = ssMat[new Rect(ssMat.Cols / 2 - 130, ssMat.Rows / 2 - 8, 5, 5)];
                var fishTopVampireCheck = ssMat[new Rect(ssMat.Cols / 2 + 22, ssMat.Rows / 2 - 54, 5, 5)];
                var fishBottomVampireCheck = ssMat[new Rect(ssMat.Cols / 2 - 13, ssMat.Rows / 2 + 52, 5, 5)];
                var fishRightVampireCheck = ssMat[new Rect(ssMat.Cols / 2 + 144, ssMat.Rows / 2 - 8, 5, 5)];


                var fishLeftVampire = VampireAround(fishLeftVampireCheck);
                var fishTopVampire = VampireAround(fishTopVampireCheck);
                var fishBottomVampire = VampireAround(fishBottomVampireCheck);
                var fishRightVampire = VampireAround(fishRightVampireCheck);

                if (!_lastRod.Contains(Rods.Left) && !fishLeftVampire)
                {
                    for (var x = 0; x < fishLeft.Cols; x++)
                    {
                        for (var y = 0; y < fishLeft.Rows; y++)
                        {
                            var pixel = fishLeft.At<Vec3b>(y, x);
                            if (!Images.PixelTolerance(pixel, _fishColor, 5)) continue;
                            // NativeImports.KeyDownLeftArrow(Window.Handle);
                            // _lastRod = Rods.Left;
                            // _holdingRod = true;
                            // new Task(() =>
                            // {
                            //     Task.Delay(405).Wait();
                            //     NativeImports.KeyUpLeftArrow(Window.Handle);
                            //     _holdingRod = false;
                            //     new Task(() =>
                            //     {
                            //         Task.Delay(_delayAfterPulling).Wait();
                            //         if (_lastRod == Rods.Left)
                            //             _lastRod = Rods.None;
                            //     }).Start();
                            // }).Start();
                            NativeImports.ClickLeftArrow(Window.Handle);
                            _lastRod.Add(Rods.Left);
                            new Task(() =>
                            {
                                Task.Delay(_delayAfterPulling).Wait();
                                _lastRod.Remove(Rods.Left);
                            }).Start();
                            return;
                        }
                    }
                }


                if (!_lastRod.Contains(Rods.Up) && !fishTopVampire)
                {
                    for (var x = 0; x < fishTop.Cols; x++)
                    {
                        for (var y = 0; y < fishTop.Rows; y++)
                        {
                            var pixel = fishTop.At<Vec3b>(y, x);
                            if (!Images.PixelTolerance(pixel, _fishColor, 5)) continue;
                            // NativeImports.KeyDownUpArrow(Window.Handle);
                            // _lastRod = Rods.Up;
                            // _holdingRod = true;
                            // new Task(() =>
                            // {
                            //     Task.Delay(405).Wait();
                            //     NativeImports.KeyUpUpArrow(Window.Handle);
                            //     _holdingRod = false;
                            //     new Task(() =>
                            //     {
                            //         Task.Delay(_delayAfterPulling).Wait();
                            //         if (_lastRod == Rods.Up)
                            //             _lastRod = Rods.None;
                            //     }).Start();
                            // }).Start();
                            NativeImports.ClickUpArrow(Window.Handle);
                            _lastRod.Add(Rods.Up);
                            new Task(() =>
                            {
                                Task.Delay(_delayAfterPulling).Wait();
                                _lastRod.Remove(Rods.Up);
                            }).Start();
                            return;
                        }
                    }
                }

                if (!_lastRod.Contains(Rods.Down) && !fishBottomVampire)
                {
                    for (var x = 0; x < fishBottom.Cols; x++)
                    {
                        for (var y = 0; y < fishBottom.Rows; y++)
                        {
                            var pixel = fishBottom.At<Vec3b>(y, x);
                            if (!Images.PixelTolerance(pixel, _fishColor, 5)) continue;
                            // NativeImports.KeyDownDownArrow(Window.Handle);
                            // _lastRod = Rods.Down;
                            // _holdingRod = true;
                            // new Task(() =>
                            // {
                            //     Task.Delay(405).Wait();
                            //     NativeImports.KeyUpDownArrow(Window.Handle);
                            //     _holdingRod = false;
                            //     new Task(() =>
                            //     {
                            //         Task.Delay(_delayAfterPulling).Wait();
                            //         if (_lastRod == Rods.Down)
                            //             _lastRod = Rods.None;
                            //     }).Start();
                            // }).Start();
                            NativeImports.ClickDownArrow(Window.Handle);
                            _lastRod.Add(Rods.Down);
                            new Task(() =>
                            {
                                Task.Delay(_delayAfterPulling).Wait();
                                _lastRod.Remove(Rods.Down);
                            }).Start();
                            return;
                        }
                    }
                }

                if (!_lastRod.Contains(Rods.Right) && !fishRightVampire)
                {
                    for (var x = 0; x < fishRight.Cols; x++)
                    {
                        for (var y = 0; y < fishRight.Rows; y++)
                        {
                            var pixel = fishRight.At<Vec3b>(y, x);
                            if (!Images.PixelTolerance(pixel, _fishColor, 5)) continue;
                            // NativeImports.KeyDownRightArrow(Window.Handle);
                            // _lastRod = Rods.Right;
                            // _holdingRod = true;
                            // new Task(() =>
                            // {
                            //     Task.Delay(405).Wait();
                            //     NativeImports.KeyUpRightArrow(Window.Handle);
                            //     _holdingRod = false;
                            //     new Task(() =>
                            //     {
                            //         Task.Delay(_delayAfterPulling).Wait();
                            //         if (_lastRod == Rods.Right)
                            //             _lastRod = Rods.None;
                            //     }).Start();
                            // }).Start();
                            NativeImports.ClickRightArrow(Window.Handle);
                            _lastRod.Add(Rods.Right);
                            new Task(() =>
                            {
                                Console.WriteLine("Before: " + DateTime.Now.Millisecond);
                                Task.Delay(_delayAfterPulling).Wait();
                                Console.WriteLine("After: " + DateTime.Now.Millisecond);
                                _lastRod.Remove(Rods.Right);
                            }).Start();
                            return;
                        }
                    }
                }

                break;
            }

            case States.QtEvent:
            {
                var fishEvent = ssMat[
                    new Rect(ssMat.Cols / 2 - 230, ssMat.Rows / 2 - 10, 5, 5)];


                var stillInEvent = false;

                for (var x = 0; x < fishEvent.Width; x++)
                {
                    if (stillInEvent) break;
                    for (var y = 0; y < fishEvent.Height; y++)
                    {
                        var pixel = fishEvent.At<Vec3b>(y, x);
                        if (!Images.PixelTolerance(pixel, _eventFishColor, 10)) continue;
                        stillInEvent = true;
                        break;
                    }
                }

                if (!stillInEvent)
                {
                    _currentState = States.Playing;
                    WaitTicks();
                    break;
                }

                if (_waitTicks-- > 0) return;

                var delay = (Window.ViewModel.EventFishDelay == 0 ? 1 : Window.ViewModel.EventFishDelay) /
                            (Window.ViewModel.UpdateInterval == 0 ? 1 : Window.ViewModel.UpdateInterval);
                WaitTicks(delay > 0 ? delay : 1);
                
                var eventFishBarMat = ssMat[new Rect(ssMat.Cols / 2 - 170, ssMat.Rows / 2 - 9, 400, 35)];

                var eventFishLeft = Images.FindTemplate(eventFishBarMat, Images.ArrowLeft);
                var eventFishRight = Images.FindTemplate(eventFishBarMat, Images.ArrowRight);
                var eventFishUp = Images.FindTemplate(eventFishBarMat, Images.ArrowUp);
                var eventFishDown = Images.FindTemplate(eventFishBarMat, Images.ArrowDown);

                var events = new[] {eventFishLeft.Value, eventFishRight.Value, eventFishUp.Value, eventFishDown.Value};

                var indexOfMax = events.ToList().IndexOf(events.Max());

                switch (indexOfMax)
                {
                    case 0:
                        NativeImports.ClickLeftArrow(Window.Handle);
                        break;
                    case 1:
                        NativeImports.ClickRightArrow(Window.Handle);
                        break;
                    case 2:
                        NativeImports.ClickUpArrow(Window.Handle);
                        break;
                    case 3:
                        NativeImports.ClickDownArrow(Window.Handle);
                        break;
                }
                
                // if (eventFishLeft.Value > 0.9)
                // {
                //     NativeImports.ClickLeftArrow(Window.Handle);
                //     break;
                // }
                //
                //
                //
                // if (eventFishRight.Value > 0.9)
                // {
                //     NativeImports.ClickRightArrow(Window.Handle);
                //     break;
                // }
                //
                //
                //
                // if (eventFishUp.Value > 0.9)
                // {
                //     NativeImports.ClickUpArrow(Window.Handle);
                //     break;
                // }
                //
                //
                // if (eventFishDown.Value > 0.9)
                // {
                //     NativeImports.ClickDownArrow(Window.Handle);
                // }

                break;
            }

            case States.GettingReward:
            {
                if (_waitTicks < -100)
                {
                    _currentState = States.SelectingLevel;
                    break;
                }

                var resultMat =
                    ssMat[
                        new Rect(ssMat.Cols / 2 - 100, ssMat.Rows / 2 - 100, 200, 70)];

                var template = Images.Result;
                var result = Images.FindTemplate(resultMat, template);

                if (result.Value < 0.8)
                {
                    NativeImports.ClickLeftArrow(Window.Handle);
                    return;
                }

                NativeImports.ClickAt(Window.Handle, new Point(ssMat.Width / 2 + 130, ssMat.Height / 2 + 50));
                WaitTicks();
                _currentState = States.SelectingLevel;
                break;
            }

            case States.SelectingLevel:
            {
                if (_waitTicks-- > 0) return;

                var resultMat =
                    ssMat[
                        new Rect(ssMat.Cols / 2 - 167, ssMat.Rows / 2 + 38, 70, 50)];

                var template = Images.LevelButton;
                var result = Images.FindTemplate(resultMat, template);
                if (result.Value < 0.8)
                {
                    WaitTicks();
                    _currentState = States.GettingReward;
                    break;
                }

                var xDiff = Window.Level switch
                {
                    1 => -140,
                    2 => -70,
                    3 => 0,
                    4 => 70,
                    5 => 140,
                    _ => 0
                };

                NativeImports.ClickAt(Window.Handle, new Point(ssMat.Width / 2 + xDiff, ssMat.Height / 2 + 55));
                WaitTicks();
                _currentState = States.Finished;
                break;
            }

            case States.Failed:
            {
                if (Points > _levelPoints[Window.Level] + 10)
                {
                    _currentState = States.GettingReward;
                    break;
                }

                if (_waitTicks-- > 0) return;

                NativeImports.ClickAt(Window.Handle, new Point(ssMat.Width / 2 - 130, ssMat.Height / 2 + 50));
                WaitTicks();
                _currentState = States.StartGame;

                break;
            }

            case States.Finished:
            {
                if (_waitTicks-- > 0) return;

                if (Window.CurrentRepeat + 1 <= Window.Repeats)
                {
                    Window.CurrentRepeat++;
                    //Start again
                    NativeImports.ClickAt(Window.Handle, new Point(ssMat.Width / 2 - 60, ssMat.Height / 2 + 80));
                    WaitTicks();
                    _currentState = States.ClosingRewardScreen;
                }
                else
                {
                    //Close game
                    NativeImports.ClickAt(Window.Handle, new Point(ssMat.Width / 2 + 60, ssMat.Height / 2 + 80));
                    StopReadPoints();
                    Window.StopBot();
                }

                break;
            }

            case States.ClosingRewardScreen:
            {
                if (_waitTicks-- > 0) return;

                var couponRefillMat =
                    ssMat[
                        new Rect(ssMat.Cols / 2 - 90, ssMat.Rows / 2 - 70, 190, 30)];

                var template = Images.NotEnoughPoints;

                var result = Images.FindTemplate(couponRefillMat, template);
                _currentState = result.Value > 0.8 ? States.CouponRefill : States.StartGame;
                WaitTicks();
                if (result.Value > 0.8)
                {
                    _currentState = States.CouponRefill;
                    NativeImports.ClickAt(Window.Handle, new Point(ssMat.Width / 2 + 145, ssMat.Height / 2 + 40));
                }
                else
                {
                    _currentState = States.StartGame;
                }

                break;
            }

            case States.CouponRefill:
            {
                switch (_couponRefillState)
                {
                    case CouponRefillStates.CloseGame:
                    {
                        if (_waitTicks-- > 0) return;

                        if (ReadPoints != null)
                        {
                            ReadPoints.Enabled = false;
                            ReadPoints?.Stop();
                            Points = 0;
                        }

                        NativeImports.ClickAt(Window.Handle, new Point(ssMat.Width / 2 + 60, ssMat.Height / 2 + 80));
                        _couponRefillState = CouponRefillStates.UseCoupon;
                        WaitTicks();
                        break;
                    }

                    case CouponRefillStates.UseCoupon:
                    {
                        if (_waitTicks-- > 0) return;

                        if (_usedCoupons > 3)
                        {
                            _couponRefillState = CouponRefillStates.Finished;
                            break;
                        }

                        NativeImports.ClickZero(Window.Handle);
                        WaitTicks();
                        _couponRefillState = CouponRefillStates.AcceptCoupon;
                        break;
                    }

                    case CouponRefillStates.AcceptCoupon:
                    {
                        if (_waitTicks-- > 0) return;

                        var couponMat =
                            ssMat[
                                new Rect(ssMat.Cols / 2 - 110, ssMat.Rows / 2 - 50, 40, 20)];

                        var template = Images.CouponCheck;
                        var result = Images.FindTemplate(couponMat, template);

                        if (result.Value > 0.8)
                        {
                            NativeImports.ClickReturn(Window.Handle);
                            _usedCoupons++;
                            _couponRefillState = CouponRefillStates.UseCoupon;
                            WaitTicks();
                        }

                        break;
                    }

                    case CouponRefillStates.Finished:
                    {
                        _currentState = States.Idle;
                        _usedCoupons = 0;
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private bool VampireAround(Mat mat)
    {
        for (var x = 0; x < mat.Cols; x++)
        {
            for (var y = 0; y < mat.Rows; y++)
            {
                var pixel = mat.At<Vec3b>(y, x);
                if (Images.PixelTolerance(pixel, _vampireColor, 20))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void WaitTicks(int optionalTicks = 0)
    {
        _waitTicks = optionalTicks == 0 ? 20 : optionalTicks;
    }
}