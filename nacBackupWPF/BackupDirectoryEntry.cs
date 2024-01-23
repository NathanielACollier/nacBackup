using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using WPFViewModelBase;

namespace nacBackupWPF
{
    public class BackupDirectoryEntry : ViewModelBase
    {

        public string Path
        {
            get { return base.GetValue(() => this.Path); }
            set { base.SetValue(() => this.Path, value); }
        }

        public string Notes
        {
            get { return base.GetValue(() => this.Notes); }
            set { base.SetValue(() => this.Notes, value); }
        }

        public string FileSize
        {
            get { return base.GetValue(() => this.FileSize); }
            set { base.SetValue(() => this.FileSize, value); }
        }

        public string BackupName
        {
            get { return base.GetValue(() => this.BackupName); }
            set { base.SetValue(() => this.BackupName, value); }
        }

        public long FileSizeBytes
        {
            get { return base.GetValue(() => this.FileSizeBytes); }
            set { base.SetValue(() => this.FileSizeBytes, value); }
        }

    }
}
