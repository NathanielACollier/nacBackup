using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  UiContext implementation for the WPF UI.  Marshals work onto the WPF
 *  dispatcher thread.
 * </summary>
 */
public class WpfUiContext : UiContext
{
    private readonly Dispatcher _dispatcher;
    private DispatcherTimer _timer;

    public WpfUiContext(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public override void RunOnUi(Action action)
    {
        _dispatcher.Invoke(action);
    }

    public override Task RunOnUiAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource<bool>();

        _dispatcher.Invoke(() =>
        {
            action().ContinueWith((task) =>
            {
                completion.TrySetResult(true);
            });
        });

        return completion.Task;
    }

    public override void StartTimer(Action onTick, int intervalMs)
    {
        StopTimer();

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        _timer.Tick += (_s, _e) => onTick();
        _timer.Start();
    }

    public override void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }
    }
}
