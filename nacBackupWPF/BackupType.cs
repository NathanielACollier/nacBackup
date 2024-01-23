using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using System.IO;

using System.Collections.ObjectModel;
using System.Xml.Linq;
using System.Threading;

using Ecark.Extensions;

using WPFViewModelBase;

namespace nacBackupWPF
{
    public class BackupType : ViewModelBase
    {


        public string TimeStampFormat
        {
            get { return base.GetValue( () => this.TimeStampFormat ); }
            set 
            {
                string newFormat = value;

                // if the format doesn't work, change it back to the default one
                if (string.IsNullOrEmpty(newFormat) || DateTime.Now.ToString(newFormat).IsDateTime() == false)
                {
                    newFormat = DefaultTimeStampFormat;
                }

                base.SetValue(() => this.TimeStampFormat, newFormat);

                OnPropertyChanged("TimeStampValue"); // update time stamp value if the format changed
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

        public ObservableCollection<BackupLocationType> BackupLocations
        {
            get { return base.GetValue(() => this.BackupLocations); }
        }


        public BackupType()
        {

            TimeStampFormat = DefaultTimeStampFormat;
        }


        public static List<BackupType> LoadBackupTypeListFromConfigFile(string configFilePath)
        {

            if (File.Exists(configFilePath))
            {
                List<BackupType> backupTypesList = new List<BackupType>();

                XDocument backupTypesDoc = XDocument.Load(configFilePath);

                foreach (XElement backupTypeElement in backupTypesDoc.Descendants("backup"))
                {

                    BackupType backupType = new BackupType
                    {
                        TimeStampFormat = (string)backupTypeElement.Attribute("TimeStamp").Value,
                        Name = (string)backupTypeElement.Attribute("BackupName").Value,
                        DestinationPath = (string)backupTypeElement.Attribute("BackupDirectory").Value
                    };

                    foreach (XElement backupLocationElement in backupTypeElement.Descendants("backupDirectory"))
                    {
                        BackupLocationType backupLocationType = new BackupLocationType
                        {
                            BackupLocationName = (string)backupLocationElement.Attribute("BackupName").Value,
                            BackupLocationPath = backupLocationElement.Value
                        };


                        backupType.BackupLocations.Add(backupLocationType);

                    }


                    backupTypesList.Add(backupType);
                }



                return backupTypesList;
            }
            else
            {
                throw new Exception(string.Format("File {0} does not exist.", configFilePath));
            }

        }




        public static void SaveBackupTypeListToConfigFile(List<BackupType> backupTypeList, string configFilePath)
        {
            XDocument xDoc = new XDocument();

            XElement root = new XElement("backups");

            foreach (var backupType in backupTypeList)
            {
                if (string.IsNullOrEmpty(backupType.DestinationPath))
                {
                    backupType.DestinationPath = string.Empty;
                }

                if (string.IsNullOrEmpty(backupType.Name))
                {
                    throw new Exception("Backup type has a null name.  All backup types must have a name.");
                }

                XElement backupTypeElement = new XElement("backup",
                                                new XAttribute("TimeStamp", backupType.TimeStampFormat),
                                                new XAttribute("BackupDirectory", backupType.DestinationPath ),
                                                new XAttribute("BackupName", backupType.Name)
                                                );

                foreach (var backupLocationType in backupType.BackupLocations)
                {
                    XElement backupLocationTypeElement = new XElement("backupDirectory",
                                                            new XAttribute("BackupName", backupLocationType.BackupLocationName),
                                                            backupLocationType.BackupLocationPath
                                                            );
                    backupTypeElement.Add(backupLocationTypeElement);
                }

                root.Add(backupTypeElement);
            }

            xDoc.Add(root);
            xDoc.Save(configFilePath);
        }






    }// end of BackupType Class
}
