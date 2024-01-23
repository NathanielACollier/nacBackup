using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using WPFViewModelBase;

namespace nacBackupWPF
{
    public class BackupLocationType : ViewModelBase
    {


        public string BackupLocationPath
        {
            get { return base.GetValue(() => this.BackupLocationPath); }
            set { base.SetValue(() => this.BackupLocationPath, value); }
        }

        public string BackupLocationName
        {
            get { return base.GetValue(() => this.BackupLocationName); }
            set { base.SetValue(() => this.BackupLocationName, value); }
        }


        public string BackupLocationSize
        {
            get { return base.GetValue(() => this.BackupLocationSize); }
            set { base.SetValue(() => this.BackupLocationSize, value); }
        }





    }// end of backup location type
}
