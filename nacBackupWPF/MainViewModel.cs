using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using WPFViewModelBase;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Diagnostics;

using System.Windows.Media;

using Ecark.Extensions;

namespace nacBackupWPF
{
    public class MainViewModel : ViewModelBase
    {

        private DispatcherTimer timer;
        private Stopwatch stopWatch;


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
            set { base.SetValue(() => this.SelectedBackup, value); }
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
                OnPropertyChanged("CompressedBytesCounterStr");
                OnPropertyChanged("CompressionRatioStr");
            }
        }

        public string CompressedBytesCounterStr
        {
            get { return CompressedBytesCounter.BytesToString(); }
        }

        public long UnCompressedBytesCounter
        {
            get { return base.GetValue(() => this.UnCompressedBytesCounter); }
            set { base.SetValue(() => this.UnCompressedBytesCounter, value);
                OnPropertyChanged("UnCompressedBytesCounterStr");
                OnPropertyChanged("CompressionRatioStr");
            }
        }

        public string UnCompressedBytesCounterStr
        {
            get { return UnCompressedBytesCounter.BytesToString(); }
        }


        public string CompressionRatioStr
        {
            get
            {
                if (UnCompressedBytesCounter > 0)
                {
                    return "{0:N2}%".With(((double)CompressedBytesCounter / (double)UnCompressedBytesCounter) * 100);
                }

                return "0%";
            }
        }


        public long BackupProgressPercent
        {
            get { return base.GetValue(() => this.BackupProgressPercent); }
            set { base.SetValue(() => this.BackupProgressPercent, value);
                OnPropertyChanged("BackupProgressBrush");
            }
        }

        public Brush BackupProgressBrush
        {
            get
            {
                if (BackupProgressPercent < 100)
                {
                    return Brushes.Blue;
                }
                else
                {
                    return Brushes.Green;
                }
            }
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






        public MainViewModel()
        {
            timer = new DispatcherTimer();
            stopWatch = new Stopwatch();
            timer.Tick += new EventHandler(timer_Tick);
            timer.Start();
            stopWatch.Reset();

            ResetCounters();
        }

        /**
         * <summary>
         *   This should be called when a backup starts
         * </summary>
         */
        public void StartBackupTimer()
        {
            stopWatch.Reset();
            stopWatch.Start();
        }

        /**
         * <summary>
         *   This should be called when the backup completes
         * </summary>
         */
        public void StopBackupTimer()
        {
            stopWatch.Stop();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            BackupTimeElapsedStr = stopWatch.Elapsed.GetTimeSpanString();
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

        public void SaveBackupTypes(string configFilePath)
        {
            BackupType.SaveBackupTypeListToConfigFile(BackupTypes.ToList(), configFilePath);
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
         *   This should be ran before doing a backup
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
}
