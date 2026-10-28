using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using nacBackupWPF.models;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  Pure, framework-agnostic XML read/write of backup sets.  This is the logic
 *  that used to live as static methods on BackupType.  Both the WPF and Avalonia
 *  UIs reach the same read/write code through here.
 * </summary>
 */
public static class BackupTypeRepository
{
    public static List<BackupType> LoadBackupTypes(string configFilePath)
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

    public static void SaveBackupTypes(List<BackupType> backupTypeList, string configFilePath)
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
                                        new XAttribute("BackupDirectory", backupType.DestinationPath),
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

    public static List<BackupType> ToList(
        System.Collections.ObjectModel.ObservableCollection<BackupType> collection)
    {
        return collection.ToList();
    }
}
