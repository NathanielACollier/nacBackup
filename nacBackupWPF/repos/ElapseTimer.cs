using System;
using nacBackupWPF.models;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  Keeps MainViewModel.BackupTimeElapsedStr up to date from
 *  MainViewModel.SharedStopWatch.  The elapsed-time ticking that used to live in
 *  MainViewModel (which made it depend on the UI framework) now lives here so the
 *  model stays framework-free.  A UiContext is required to marshal a tick onto
 *  the UI thread.
 * </summary>
 */
public class ElapseTimer
{
    private readonly MainViewModel model;
    private readonly UiContext ui;
    private readonly int intervalMs;

    public ElapseTimer(MainViewModel model, UiContext ui, int intervalMs = 100)
    {
        this.model = model;
        this.ui = ui;
        this.intervalMs = intervalMs;
    }

    public void Start()
    {
        ui.StopTimer();
        ui.StartTimer(OnTick, intervalMs);
    }

    public void Stop()
    {
        ui.StopTimer();
    }

    private void OnTick()
    {
        model.BackupTimeElapsedStr = nacBackupWPF.lib.TimeSpanUtility.GetTimeSpanText(model.SharedStopWatch.Elapsed);
    }
}
