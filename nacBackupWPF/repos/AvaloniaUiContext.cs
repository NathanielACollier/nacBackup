using System;
using System.Threading;
using System.Threading.Tasks;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  UiContext implementation for the Avalonia / nac.Forms UI.  Marshals work onto
 *  the Avalonia UI thread via the global dispatcher.
 * </summary>
 */
public class AvaloniaUiContext : UiContext
{
    private Timer _timer;

    public override void RunOnUi(Action action)
    {
        var dispatcher = Avalonia.Threading.Dispatcher.UIThread;

        // Run inline when already on the UI thread so a nested RunOnUi call does
        // not block on itself.
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.InvokeAsync(action).GetAwaiter().GetResult();
    }

    public override Task RunOnUiAsync(Func<Task> action)
    {
        var dispatcher = Avalonia.Threading.Dispatcher.UIThread;

        if (!dispatcher.CheckAccess())
        {
            return dispatcher.InvokeAsync(action);
        }

        return action();
    }

    public override void StartTimer(Action onTick, int intervalMs)
    {
        StopTimer();

        _timer = new Timer(
            _ =>
            {
                // Marshal the tick back onto the UI thread.
                var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
                if (dispatcher.CheckAccess())
                {
                    onTick();
                }
                else
                {
                    dispatcher.Post(onTick);
                }
            },
            null,
            intervalMs,
            intervalMs);
    }

    public override void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
