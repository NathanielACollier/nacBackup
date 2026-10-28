using System;

namespace nacBackupWPF.models;

public class BackupType : nac.ViewModelBase.ViewModelBase
{


    public string TimeStampFormat
    {
        get { return base.GetValue( () => this.TimeStampFormat ); }
        set
        {
            string newFormat = value;

            // if the format doesn't work, change it back to the default one
            if (string.IsNullOrEmpty(newFormat) ||
                !DateTime.TryParse(s: DateTime.Now.ToString(newFormat), out DateTime parseResult)
                )
            {
                newFormat = DefaultTimeStampFormat;
            }

            base.SetValue(() => this.TimeStampFormat, newFormat);

            base.OnPropertyChanged("TimeStampValue"); // update time stamp value if the format changed
        }
    }

    public string TimeStampValue
    {
        get
        {
            return DateTime.Now.ToString(TimeStampFormat);
        }
    }

    public string DefaultTimeStampFormat
    {
        get { return "MM.dd.yyyy_hh.mm.sstt"; }
    }

    public string DestinationPath
    {
        get { return base.GetValue( () => this.DestinationPath ); }
        set { base.SetValue( () => this.DestinationPath, value ); }
    }

    public string DestinationPathSize
    {
        get { return base.GetValue( () => this.DestinationPathSize ); }
        set { base.SetValue( () => this.DestinationPathSize, value ); }
    }

    public string Name
    {
        get { return base.GetValue(() => this.Name); }
        set { base.SetValue(() => this.Name, value); }
    }

    public System.Collections.ObjectModel.ObservableCollection<BackupLocationType> BackupLocations
    {
        get { return base.GetValue(() => this.BackupLocations); }
    }


    public BackupType()
    {

        TimeStampFormat = DefaultTimeStampFormat;
    }


}
