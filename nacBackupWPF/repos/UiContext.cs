using System;
using System.Threading.Tasks;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  A tiny interface the repositories use to get onto the correct UI thread
 *  without knowing which UI framework (WPF or Avalonia) is running.
 * </summary>
 */
public abstract class UiContext
{
    /**
     * <summary>
     *  Run an action on the UI thread and block until it has completed.
     * </summary>
     */
    public abstract void RunOnUi(Action action);

    /**
     * <summary>
     *  Start a (possibly async) operation on the UI thread and return a task
     *  that completes when that operation finishes.
     * </summary>
     */
    public abstract Task RunOnUiAsync(Func<Task> action);

    /**
     * <summary>
     *  Start a repeating timer.  onTick is invoked on the UI thread every
     *  intervalMs.  Used by the ElapseTimer repo to keep BackupTimeElapsedStr
     *  up to date.
     * </summary>
     */
    public abstract void StartTimer(Action onTick, int intervalMs);

    /**
     * <summary>
     *  Stop the repeating timer started with StartTimer.
     * </summary>
     */
    public abstract void StopTimer();
}
