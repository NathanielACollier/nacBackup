using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace nacBackupWPF.models;

public class BackupDirectoryViewModel : nac.ViewModelBase.ViewModelBase
{

    public System.Collections.ObjectModel.ObservableCollection<BackupDirectoryEntry> BackupEntries
    {
        get { return base.GetValue(() => this.BackupEntries); }
    }

    /**
     * <summary>
     *  The directory the browser is currently scanning.  Bound to the directory
     *  picker in both the WPF and Avalonia browser windows.
     * </summary>
     */
    public string DirectoryPath
    {
        get { return base.GetValue(() => this.DirectoryPath); }
        set { base.SetValue(() => this.DirectoryPath, value); }
    }

    public bool IsBusy
    {
        get { return base.GetValue(() => this.IsBusy); }
        set { base.SetValue(() => this.IsBusy, value); }
    }


    public List<BackupDirectoryEntry> EntriesToDisplay
    {
        get
        {
            return BackupEntries.Where(entry => string.Equals(entry.BackupName, SelectedBackupName, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    /**
     * <summary>
     *   This sums up the size of every backup in the selected backup type
     * </summary>
     */
    public string SelectedBackupTypeTotalSize
    {
        get
        {
            long size = EntriesToDisplay.Select(entry => entry.FileSizeBytes).Sum();
            return nacBackupWPF.lib.ByteSize.BytesToString(size);
        }
    }

    /**
     * <summary>
     *   This sums up the size of every backup in the folder
     * </summary>
     */
    public string TotalBackupsSize
    {
        get
        {
            long size = BackupEntries.Select(entry => entry.FileSizeBytes).Sum();
            return nacBackupWPF.lib.ByteSize.BytesToString(size);
        }
    }


    public string SelectedBackupName
    {
        get { return base.GetValue(() => this.SelectedBackupName); }
        set { base.SetValue(() => this.SelectedBackupName, value);
                SelectedBackupTypeChanged();
        }
    }

    public List<string> BackupNames
    {
        get
        {
            return BackupEntries.Select(entry => entry.BackupName).Distinct().ToList();
        }
    }


    public void SelectedBackupTypeChanged()
    {
        base.OnPropertyChanged("SelectedBackupTypeTotalSize");
        base.OnPropertyChanged("EntriesToDisplay");
    }


    /**
     * <summary>
     * This needs to be refreshed when the BackupEntries collection is changed
     * </summary>
     */
    public void RefreshBackupEntryes()
    {
        base.OnPropertyChanged("EntriesToDisplay");
        base.OnPropertyChanged("BackupNames");
        base.OnPropertyChanged("SelectedBackupTypeTotalSize");
        base.OnPropertyChanged("TotalBackupsSize");
    }

}
