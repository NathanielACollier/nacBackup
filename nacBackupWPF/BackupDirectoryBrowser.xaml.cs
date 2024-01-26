using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.IO;

using Ionic.Zip;
using System.Threading;
using System.Windows.Threading;

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace nacBackupWPF
{
    /// <summary>
    /// Interaction logic for BackupDirectoryBrowser.xaml
    /// </summary>
    public partial class BackupDirectoryBrowser : Window
    {

        private int numberOfBusy = 0;

        private void ShowBusy()
        {
            if (numberOfBusy == 0)
            {
                BusyIndicator.ShowBusy();
            }

            ++numberOfBusy;

        }

        private void HideBusy()
        {
            if (BusyIndicator.Visibility == System.Windows.Visibility.Visible && numberOfBusy > 0 && --numberOfBusy == 0)
            {
                BusyIndicator.HideBusy();

            }
        }



        public BackupDirectoryBrowser()
        {
            InitializeComponent();
        }

        private void BackupDirectoryPicker_DirectoryPathChanged(object sender, EventArgs e)
        {
            

            if (Directory.Exists(BackupDirectoryPicker.DirectoryPath))
            {

                PopulateDirectoryEntries(BackupDirectoryPicker.DirectoryPath);
            }
        }



        private void PopulateDirectoryEntries(string directoryPath)
        {
            BackupDirectoryViewModel model = (BackupDirectoryViewModel)this.DataContext;

            const string backupFileNamePattern = @"(?<BackupName>.*)_(?<DateTime>\d+.\d+.\d+_\d+.\d+.\d+)(?<PostCode>\w+)(?<Extension>\.\w+)";

            

            ShowBusy();

            Thread getDirectoryEntriesThread = new Thread(delegate()
            {

                // do a query to get all zip files in the directory
                var zipFilesQuery = from fileInfo in Directory.GetFiles(directoryPath)
                                                              .Select(file => new FileInfo(file))
                                    where string.Equals(fileInfo.Extension, ".zip", StringComparison.OrdinalIgnoreCase)
                                    orderby fileInfo.LastWriteTime descending
                                    let match = Regex.Match( fileInfo.Name, backupFileNamePattern )
                                    select new
                                    {
                                        FileInformation = fileInfo,
                                        Date = match.Groups["DateTime"].Value,
                                        BackupName = match.Groups["BackupName"].Value,
                                        PostCode = match.Groups["PostCode"].Value,
                                        Extension = match.Groups["Extension"].Value
                                    };

                
                foreach (var entry in zipFilesQuery)
                {
                    string notes = string.Empty;

                    // we need to read the notes
                    using (ZipFile zip = ZipFile.Read(entry.FileInformation.FullName))
                    {
                        try
                        {
                            using (Stream reader = zip["notes.txt"].OpenReader())
                            {
                                byte[] data = new byte[reader.Length];
                                reader.Read(data, 0, (int)reader.Length);
                                notes = Encoding.ASCII.GetString(data);
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }

                    BackupEntriesListView.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                        {
                            model.BackupEntries.Add(new BackupDirectoryEntry
                            {
                                Path = entry.FileInformation.FullName,
                                Notes = notes,
                                FileSize = entry.FileInformation.Length.BytesToString(),
                                FileSizeBytes = entry.FileInformation.Length,
                                BackupName = entry.BackupName
                            });

                            // refresh everything as we go along
                            model.RefreshBackupEntryes();
                            // set the type to be viewed to be the current type
                            model.SelectedBackupName = entry.BackupName;
                        }));




                }




                this.Dispatcher.Invoke(new Action(delegate()
                {
                    HideBusy();
                }));

            });

            getDirectoryEntriesThread.Start();
        }

        private void OpenBackupEntryButton_Click(object sender, RoutedEventArgs e)
        {
            BackupDirectoryEntry entry = (BackupDirectoryEntry)((FrameworkElement)sender).DataContext;

            Process.Start(entry.Path);
        }




    }// end of BackupDirectoryBrowser window class
}// end of namespace
