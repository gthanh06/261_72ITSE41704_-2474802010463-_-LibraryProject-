using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// One shared frame clock for every animation in the app. A background thread waits for the next screen
/// refresh (DwmFlush, so frames follow the monitor's 60/120/144 Hz) and posts one frame to the UI thread;
/// a new frame is only posted once the previous one has run, so a busy UI thread never gets a backlog.
/// Time is measured with <see cref="Stopwatch"/>. The thread sleeps while no animation is running.
/// </summary>
internal static class AnimationClock
{
    private const double MinFrameMs = 6.0;        // cap ≈ 165 fps if DwmFlush returns early
    private const double FallbackFrameMs = 1000.0 / 60;

    private static readonly Stopwatch Watch = Stopwatch.StartNew();
    private static readonly List<Action<double>> Subscribers = new();   // UI thread only
    private static readonly AutoResetEvent Wake = new(false);
    private static SynchronizationContext? _ui;
    private static Thread? _thread;
    private static volatile bool _active;
    private static int _framePending;

    /// <summary>Milliseconds since the app started (high resolution).</summary>
    public static double NowMs => Watch.Elapsed.TotalMilliseconds;

    /// <summary>Calls <paramref name="frame"/> on every display frame until <see cref="Unsubscribe"/>. UI thread only.</summary>
    public static void Subscribe(Action<double> frame)
    {
        _ui = SynchronizationContext.Current ?? _ui;
        if (_ui == null)
        {
            throw new InvalidOperationException("Animations must be started on the UI thread.");
        }

        Subscribers.Add(frame);
        if (_thread == null)
        {
            _thread = new Thread(Loop) { IsBackground = true, Name = "Animation clock", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
        }

        if (!_active)
        {
            _active = true;
            Wake.Set();
        }
    }

    public static void Unsubscribe(Action<double> frame)
    {
        Subscribers.Remove(frame);
        if (Subscribers.Count == 0)
        {
            _active = false;
        }
    }

    private static void RunFrame()
    {
        Interlocked.Exchange(ref _framePending, 0);
        if (Subscribers.Count == 0)
        {
            return;
        }

        var now = NowMs;
        // Snapshot: a frame callback may start or stop animations.
        foreach (var frame in Subscribers.ToArray())
        {
            frame(now);
        }
    }

    private static void Loop()
    {
        var highResolution = false;
        var last = NowMs;
        while (true)
        {
            if (!_active)
            {
                if (highResolution)
                {
                    TimeEndPeriod(1);
                    highResolution = false;
                }

                Wake.WaitOne();
                continue;
            }

            if (!highResolution)
            {
                TimeBeginPeriod(1);  // makes Sleep accurate to ~1 ms while animating
                highResolution = true;
                last = NowMs;
            }

            WaitForNextFrame(last);
            last = NowMs;

            if (Interlocked.Exchange(ref _framePending, 1) == 0)
            {
                try
                {
                    _ui?.Post(_ => RunFrame(), null);
                }
                catch (Exception)
                {
                    // The UI thread is gone (app closing); just stop posting.
                    Interlocked.Exchange(ref _framePending, 0);
                    _active = false;
                }
            }
        }
    }

    private static void WaitForNextFrame(double last)
    {
        var vsynced = false;
        try
        {
            vsynced = DwmFlush() == 0;
        }
        catch (Exception)
        {
            // dwmapi missing: use the timer fallback below.
        }

        var target = vsynced ? MinFrameMs : FallbackFrameMs;
        var remaining = target - (NowMs - last);
        if (remaining > 0.5)
        {
            Thread.Sleep((int)Math.Ceiling(remaining));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint period);
}
