using System;
using System.Timers;
using NosGame.MVVM;
using NosGame.Utils;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;

namespace NosGame.MiniGames;

public class Sawmill : BaseGame
{
    public sealed override NosTaleWindow Window { get; set; }
    public override int Points { get; set; }

    protected override Timer? ReadPoints { get; set; }

    private int _waitTicks = 20;
    
    private readonly Vec3b _woodColor = new(56, 144, 199);

    private readonly int[] _levelPoints = {0, 1000, 5000, 10000, 14000, 18000};

    public Sawmill(NosTaleWindow window)
    {
        Window = window;
    }

    private States _currentState = States.Idle;
    private CouponRefillStates _couponRefillState = CouponRefillStates.CloseGame;
    private int _usedCoupons;

    public override void Update()
    {
        var screenshot = NativeImports.GetWindowScreenshot(Window.Handle);
        var ssMat = screenshot.ToMat();

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
                if (result.Value < 0.8) return;


                var loc = new Point(ssMat.Width / 2 - ssMat2.Width / 2 + result.Location.X + 10,
                    ssMat.Height / 2 + ssMat2.Height / 2 + result.Location.Y + 25);
                NativeImports.MoveTo(Window.Handle, loc);
                NativeImports.ClickAt(Window.Handle, loc);

                _currentState = States.Playing;
                break;
            }

            case States.Playing:
            {
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
                
                var topWood = ssMat[new Rect(ssMat.Cols / 2 - 65, ssMat.Rows / 2 + 13, 12, 5)];
                var bottomWood = ssMat[new Rect(ssMat.Cols / 2 - 65, ssMat.Rows / 2 + 13 + 103, 12, 5)];

                var topFlag = false;
                for (var x = 0; x < topWood.Width; x++)
                {
                    if (topFlag) break;
                    for (var y = 0; y < topWood.Height; y++)
                    {
                        var color = topWood.At<Vec3b>(y, x);
                        if (!Images.PixelTolerance(color, _woodColor, 15)) continue;
                        NativeImports.ClickLeftArrow(Window.Handle);
                        topFlag = true;
                        break;
                    }
                }
                var botFlag = false;
                for (var x = 0; x < bottomWood.Width; x++)
                {
                    if (botFlag) break;
                    for (var y = 0; y < bottomWood.Height; y++)
                    {
                        var color = bottomWood.At<Vec3b>(y, x);
                        if (!Images.PixelTolerance(color, _woodColor, 15)) continue;
                        NativeImports.ClickRightArrow(Window.Handle);
                        botFlag = true;
                        break;
                    }
                }

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

            case States.GettingReward:
            {
                if (_waitTicks-- > 0) return;

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
                
                if (result.Value < 0.8) return;
                
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
            case States.QtEvent:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void WaitTicks()
    {
        _waitTicks = 20;
    }
}