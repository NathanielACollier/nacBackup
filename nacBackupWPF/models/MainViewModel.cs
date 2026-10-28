using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace nacBackupWPF.models;

public class MainViewModel : nac.ViewModelBase.ViewModelBase
{

    public string BackupSetPath
    {
        get { return base.GetValue(() => this.BackupSetPath); }
        set { base.SetValue(() => this.BackupSetPath, value); }
    }

    public ObservableCollection<BackupType> BackupTypes
    {
        get { return base.GetValue(() => this.BackupTypes); }
    }

    public BackupType SelectedBackup
    {
        get { return base.GetValue(() => this.SelectedBackup); }
        set
        {
            base.SetValue(() => this.SelectedBackup, value);
            // a new selection exposes a (possibly different) BackupLocations collection
            base.OnPropertyChanged("SelectedBackupLocations");
        }
    }

    /**
     * <summary>
     *  The backup locations of the currently selected backup.  Virtual so it is
     *  null-safe when nothing is selected.
     * </summary>
     */
    public ObservableCollection<BackupLocationType> SelectedBackupLocations
    {
        get
        {
            return SelectedBackup == null ? null : SelectedBackup.BackupLocations;
        }
    }

    public BackupLocationType SelectedBackupLocation
    {
        get { return base.GetValue(() => this.SelectedBackupLocation); }
        set { base.SetValue(() => this.SelectedBackupLocation, value); }
    }

    public long CompressedBytesCounter
    {
        get { return base.GetValue(() => this.CompressedBytesCounter); }
        set { base.SetValue(() => this.CompressedBytesCounter, value);
            base.OnPropertyChanged("CompressedBytesCounterStr");
            base.OnPropertyChanged("CompressionRatioStr");
        }
    }

    public string CompressedBytesCounterStr
    {
        get { return nacBackupWPF.lib.ByteSize.BytesToString(CompressedBytesCounter); }
    }

    public long UnCompressedBytesCounter
    {
        get { return base.GetValue(() => this.UnCompressedBytesCounter); }
        set { base.SetValue(() => this.UnCompressedBytesCounter, value);
            base.OnPropertyChanged("UnCompressedBytesCounterStr");
            base.OnPropertyChanged("CompressionRatioStr");
        }
    }

    public string UnCompressedBytesCounterStr
    {
        get { return nacBackupWPF.lib.ByteSize.BytesToString(UnCompressedBytesCounter); }
    }

    public string CompressionRatioStr
    {
        get
        {
            if (UnCompressedBytesCounter > 0)
            {
                double ratio = ((double)CompressedBytesCounter / (double)UnCompressedBytesCounter) * 100;
                return $"{ratio:N2}%";
            }

            return "0%";
        }
    }

    public long BackupProgressPercent
    {
        get { return base.GetValue(() => this.BackupProgressPercent); }
        set { base.SetValue(() => this.BackupProgressPercent, value); }
    }

    public int FileCount
    {
        get { return base.GetValue(() => this.FileCount); }
        set { base.SetValue(() => this.FileCount, value); }
    }

    public int DirectoryCount
    {
        get { return base.GetValue(() => this.DirectoryCount); }
        set { base.SetValue(() => this.DirectoryCount, value); }
    }

    /**
     * <summary>
     *  Used to display how long it has taken to backup something
     * </summary>
     */
    public string BackupTimeElapsedStr
    {
        get { return base.GetValue(() => this.BackupTimeElapsedStr); }
        set { base.SetValue(() => this.BackupTimeElapsedStr, value); }
    }

    public bool IsBusy
    {
        get { return base.GetValue(() => this.IsBusy); }
        set { base.SetValue(() => this.IsBusy, value); }
    }

    /**
     * <summary>
     *  Plain-text backup log.  Shared by the backup engine; written from a
     *  background thread and marshalled to the UI by the calling repo.
     * </summary>
     */
    public string Log
    {
        get { return base.GetValue(() => this.Log); }
        set { base.SetValue(() => this.Log, value); }
    }

    /**
     * <summary>
     *  Free-form notes the user enters.  Persisted to and restored from the
     *  notes.txt file by the notes repository.
     * </summary>
     */
    public string Notes
    {
        get { return base.GetValue(() => this.Notes); }
        set { base.SetValue(() => this.Notes, value); }
    }

    /**
     * <summary>
     *  When true the active backup should be stopped as soon as it notices.
     * </summary>
     */
    public bool Stop
    {
        get { return base.GetValue(() => this.Stop); }
        set { base.SetValue(() => this.Stop, value); }
    }

    /**
     * <summary>
     *  Shared by the form and the backup engine so the elapsed time can be read
     *  even after the timer has stopped.
     * </summary>
     */
    public Stopwatch SharedStopWatch = new();

    public MainViewModel()
    {
        ResetCounters();
    }

    /**
     * <summary>
     *  This should be called when a backup starts
     * </summary>
     */
    public void StartBackupTimer()
    {
        SharedStopWatch.Reset();
        SharedStopWatch.Start();
    }

    /**
     * <summary>
     *  This should be called when the backup completes
     * </summary>
     */
    public void StopBackupTimer()
    {
        SharedStopWatch.Stop();
    }

    /**
     * <summary>
     *  Will clear out the current list, and load the new list
     *  Then sets the selected item to be the first one
     * </summary>
     */
    public void LoadBackupTypes(List<BackupType> backupTypeList)
    {

        BackupTypes.Clear();
        foreach (var backupType in backupTypeList)
        {
            BackupTypes.Add(backupType);
        }

        // set the selected backup to be the first one if there are any backups
        if (BackupTypes.Count > 0)
        {
            SelectedBackup = BackupTypes.First();
        }
    }

    public void ClearBackups()
    {
        BackupTypes.Clear();
        SelectedBackup = null;

        StopBackupTimer();
        ResetCounters();

        BackupSetPath = null;
    }

    /**
     * <summary>
     *  This should be ran before doing a backup
     * </summary>
     */
    public void ResetCounters()
    {
        CompressedBytesCounter = 0;
        UnCompressedBytesCounter = 0;
        FileCount = 0;
        DirectoryCount = 0;
        BackupProgressPercent = 0;
    }

}
