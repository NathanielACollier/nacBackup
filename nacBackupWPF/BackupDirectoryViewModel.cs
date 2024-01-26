using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections.ObjectModel;

namespace nacBackupWPF
{
    public class BackupDirectoryViewModel : nac.ViewModelBase.ViewModelBase
    {

        public ObservableCollection<BackupDirectoryEntry> BackupEntries
        {
            get { return base.GetValue(() => this.BackupEntries); }
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
                return lib.ByteSize.BytesToString(size);
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
                return lib.ByteSize.BytesToString(size);
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
            OnPropertyChanged("SelectedBackupTypeTotalSize");
            OnPropertyChanged("EntriesToDisplay");
        }


        /**
         * <summary>
         * This needs to be refreshed when the BackupEntries collection is changed
         * </summary>
         */
        public void RefreshBackupEntryes()
        {
            OnPropertyChanged("EntriesToDisplay");
            OnPropertyChanged("BackupNames");
            OnPropertyChanged("SelectedBackupTypeTotalSize");
            OnPropertyChanged("TotalBackupsSize");
        }

    }
}
