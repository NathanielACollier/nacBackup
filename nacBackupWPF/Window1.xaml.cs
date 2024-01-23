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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.ComponentModel;
using System.Windows.Threading;
using System.IO;
using Ionic.BZip2;
using Ionic.Zip;
using System.Windows.Markup;

using Ecark.Extensions;

using NCWPFExtensions;

namespace nacBackupWPF
{



    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        #region Stop DP

        public static readonly DependencyProperty StopProperty = DependencyProperty.Register("Stop", typeof(bool), typeof(Window1), new PropertyMetadata(false));

        public bool Stop
        {
            get { return this.GetValueThreadSafe<bool>(StopProperty); }
            set { this.SetValueThreadSafe(StopProperty, value); }
        }

        #endregion



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

        public Brush DefaultButtonBackGround
        {
            get { return (new Button()).Background; }
        }


        public string NotesFilePath
        {
            get { return string.Format(@"{0}\notes.txt", Environment.CurrentDirectory); }
        }

        public string LogFilePath
        {
            get { return string.Format(@"{0}\log.rtf", Environment.CurrentDirectory); }
        }

        public string ApplicationStartPath
        {
            get { return System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName); }
        }


        public Window1()
        {
            InitializeComponent();

            PopulateNotesTextBoxFromNotesFile();

            

            //*** no longer automaticly loading a backup set when the application launches


            // use of FlowDocument to write to rich textbox
            //http://stackoverflow.com/questions/957441/richtextbox-wpf-does-not-have-string-property-text
            //  I also added some extension methods in, and a NewParagraph method since there appear to be no extension constructors in c#
            LogTextBox.Document.AddParagraphs( new Paragraph(
                new Run
                {
                    FontFamily = new FontFamily("Arial"),
                    Foreground = new SolidColorBrush(Colors.Green),
                    Text = "Application Started.  Now logging.  Write log information here."
                }));
            
        }


        /**
         * <summary>
         *  Populates the notes text from file
         *  If the file is empty it gets deleted
         * </summary>
         */
        private void PopulateNotesTextBoxFromNotesFile()
        {
            if (File.Exists(NotesFilePath))
            {
                string notes = File.ReadAllText(NotesFilePath);

                if (string.IsNullOrEmpty(notes))
                {
                    File.Delete(NotesFilePath);
                }
                else
                {
                    NotesTextBox.Text = notes;
                }
            }
        }




        private void NewBackupButton_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            model.BackupTypes.Add(new BackupType());

            model.SelectedBackup = model.BackupTypes.Last(); // should select the one we just added
        }

        /**
         * <summary>
         *  This removes the backup that is selected by the drop down from the backup list then re displays the form
         * </summary>
         */
        private void RemoveBackupButton_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            if (model.SelectedBackup != null)
            {
                model.BackupTypes.Remove(model.SelectedBackup);

                model.SelectedBackup = null;
            }

        }







        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            this.Stop = false;

            if (model.SelectedBackup != null)
            {


                Clear();


                

                // Check a few things before gathering more information to perform the backup
                if (model.SelectedBackup.Name != null && model.SelectedBackup.DestinationPath != null && Directory.Exists(model.SelectedBackup.DestinationPath) && model.SelectedBackup.BackupLocations.Count >= 1)
                {


                    model.StartBackupTimer();

                    using (ZipFile zip = new ZipFile())
                    {
                        zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;

                        zip.CompressionMethod = CompressionMethod.BZip2;


                        string zipFileName = @"{0}\{1}_{2}.zip".With(model.SelectedBackup.DestinationPath,
                                                                        model.SelectedBackup.Name,
                                                                        model.SelectedBackup.TimeStampValue);

                        LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                        {
                            Foreground = Brushes.Purple,
                            FontSize = 12.0f,
                            Text = string.Format("\tBackup Filename: {0}", zipFileName)
                        }));


                        zip.SaveProgress += new EventHandler<SaveProgressEventArgs>(zip_SaveProgress);

                        // collect all of the non blank backupLocations
                        var validBackupLocations = (from backupLocation in model.SelectedBackup.BackupLocations
                                                    where !string.IsNullOrEmpty(backupLocation.BackupLocationName) &&
                                                        !string.IsNullOrEmpty(backupLocation.BackupLocationPath) &&
                                                        Directory.Exists(backupLocation.BackupLocationPath)
                                                    select backupLocation).ToList();
                        // loop through the valid backups and add them to the zip file  We have allready checked to make sure the directories exist
                        foreach (var backupLocation in validBackupLocations)
                        {
                            zip.AddDirectory(backupLocation.BackupLocationPath, backupLocation.BackupLocationName);
                        }

                        // start a new thread to save the zip file
                        Thread saveZipFileThread = new Thread((ThreadStart)delegate()
                        {
                            try
                            {
                                zip.Save(zipFileName);
                            }
                            catch (Exception ex)
                            {
                                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                                    {
                                        LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                                        {
                                            Foreground = Brushes.Red,
                                            FontSize = 12.0f,
                                            Text = string.Format("Error backing up {0}.  Exception message: {1}", model.SelectedBackup.Name, ex.Message)
                                        }));

                                        // stop the backup timer since something went wrong with the backup
                                        model.StopBackupTimer();

                                    }));
                            }



                            // put the log in an rtf file and add it to the zip   "log.rtf"
                            SaveLogToZip(zipFileName);
                            // put the notes in a text file and add it to the zip "notes.txt"
                            SaveNotesToZip(zipFileName);

                        });

                        saveZipFileThread.Start();

                    }
                }
                else // something is wrong with the backup type
                {
                    var errorMessage = string.Empty;

                    if (!Directory.Exists(model.SelectedBackup.DestinationPath))
                    {
                        errorMessage += "Directory {0} does not exist".With(model.SelectedBackup.DestinationPath);
                    }

                    if (model.SelectedBackup.BackupLocations.Count < 1)
                    {
                        errorMessage += "There are no backup locations to backup";
                    }

                    if (model.SelectedBackup.Name == null || model.SelectedBackup.DestinationPath == null)
                    {
                        errorMessage += "Something is wrong with the backup name({0}) or the destination path({1})".With(model.SelectedBackup.Name, model.SelectedBackup.DestinationPath);
                    }

                    LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                    {
                        Text = errorMessage,
                        Foreground = Brushes.Red
                    }));


                }

            }
            else // nothing was selected
            {
                LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                {
                    Text = "You did not select a backup type from the list",
                    Foreground = Brushes.Red
                }));
            }

        }

        /**
         * <summary>
         *  This event occurs everytime there is some kind of progress with the zipfile saving
         * </summary>
         */
        void zip_SaveProgress(object sender, SaveProgressEventArgs e)
        {
            MainViewModel model = null;

            if (this.Stop == true)
            {
                e.Cancel = true;
                return;
            }

            this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                model = (MainViewModel)this.DataContext;
            }));

            if (e.EventType == ZipProgressEventType.Saving_AfterWriteEntry)
            {
                
                // Set the backup progress bar value
                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                {
                    model.BackupProgressPercent = (int)(((double)e.EntriesSaved / (double)e.EntriesTotal) * 100);
                }));

                if (e.CurrentEntry.IsDirectory == true)
                {

                    this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        // write out the directory name
                        // I had to create the paragraph inside the invoke method for some reason, if I didn't it gave an ominous exception 


                        LogTextBox.Document.AddParagraphs((new Paragraph()).AddInlines(
                        new Run
                        {
                            FontFamily = new FontFamily("Arial Black"),
                            FontSize = 12F,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.Green,
                            Text = "Backing up Directory: "
                        },
                        new Run
                        {
                            FontFamily = new FontFamily("Arial"),
                            FontSize = 10F,
                            Foreground = Brushes.Blue,
                            Text = "{0}".With( e.CurrentEntry.FileName )
                        }));

                        // incriment the Directory count
                        model.DirectoryCount += 1;

                    }));

                }
                else // it is a file instead of a directory
                {
                    // write out the file name
                    this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {

                        LogTextBox.Document.AddParagraphs(new Paragraph(
                            new Run
                            {
                                Text = "\t\t{0}".With( e.CurrentEntry.FileName )
                            }));

                        // incriment the file count
                        model.FileCount += 1;

                        // incriment sizes for the file
                        model.CompressedBytesCounter += e.CurrentEntry.CompressedSize;
                        model.UnCompressedBytesCounter += e.CurrentEntry.UncompressedSize;

                    }));

                }// end of if is file

                

            } // end of if after writing entry to the zip file
            else if (e.EventType == ZipProgressEventType.Saving_Completed)
            {

                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        // update the progress percent
                        model.BackupProgressPercent = 100;

                        // write out a message that the backup succeded

                        LogTextBox.Document.AddParagraphs(new Paragraph(
                            new Run
                            {
                                Foreground = Brushes.Red,
                                FontWeight = FontWeights.Bold,
                                FontFamily = new FontFamily( "Arial Black" ),
                                FontSize = 14F,
                                Text = "Backup Is Now Complete !!!"
                            }));

                        // we are done so stop the backup timer
                        model.StopBackupTimer();

                        // write out the time it took to perform the backup
                        LogTextBox.Document.AddParagraphs(new Paragraph(
                            new Run
                            {
                                Foreground = Brushes.Purple,
                                Text = string.Format("Backup completed in {0}", model.BackupTimeElapsedStr)
                            }));

                        // the compressed size, uncompressed size, and compression ratio should be appended onto the end of the log
                        LogTextBox.Document.AddParagraphs(new Paragraph(
                            new Run
                            {
                                Foreground = Brushes.Purple,
                                Text = string.Format("Compressed Size: {0}\tUnCompressed Size: {1}\tCompression Ratio: {2}", model.CompressedBytesCounterStr,
                                                                                                                             model.UnCompressedBytesCounterStr,
                                                                                                                             model.CompressionRatioStr )
                            }));
                    
                    }));
                

            }// end of if saving complete

        }


        /**
         * <summary>
         *   Assumption is that this is running in the zip thread
         * </summary>
         */
        private void SaveLogToZip(string zipFileName)
        {
            try
            {
                LogTextBox.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        var content = new TextRange(LogTextBox.Document.ContentStart, LogTextBox.Document.ContentEnd);

                        using (var fileOut = new FileStream(LogFilePath, FileMode.Create))
                        {
                            content.Save(fileOut, DataFormats.Rtf);
                        }
                    }));

                
                using (ZipFile zip = ZipFile.Read(zipFileName))
                {
                    zip.AddFile(LogFilePath, "");
                    zip.Save();
                }

                File.Delete(LogFilePath);
            }
            catch (Exception ex)
            {
                LogTextBox.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                {
                    LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                    {
                        Foreground = Brushes.Red,
                        FontSize = 12.0f,
                        Text = string.Format("Error Saving log to {0}.  Exception message: {1}", zipFileName, ex.Message)
                    }));
                }));

            }
        }

        /**
         * <summary>
         *  Assumption is that this is running in the zip thread
         * </summary>
         */
        private void SaveNotesToZip(string zipFileName)
        {
            try
            {
                NotesTextBox.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                    {
                        File.WriteAllText(NotesFilePath, NotesTextBox.Text);
                    }));

                using (ZipFile zip = ZipFile.Read(zipFileName))
                {
                    zip.AddFile(NotesFilePath, "");
                    zip.Save();
                }

                ClearNotes();
            }
            catch (Exception ex)
            {
                NotesTextBox.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                {
                    LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                    {
                        Foreground = Brushes.Red,
                        FontSize = 12.0f,
                        Text = string.Format("Error Saving notes to {0}.  Exception message: {1}", zipFileName, ex.Message)
                    }));
                }));

            }
        }




        private void AddBackupLocationButton_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            if (model.SelectedBackup != null)
            {
                model.SelectedBackup.BackupLocations.Add(new BackupLocationType());
            }
        }

        private void RemoveBackupLocationButton_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            if (model.SelectedBackup != null && model.SelectedBackupLocation != null)
            {
                model.SelectedBackup.BackupLocations.Remove(model.SelectedBackupLocation);


                model.SelectedBackupLocation = null;
            }
            else
            {
                System.Windows.MessageBox.Show("Error removing backup location.  Either a backup or a location is not selected.", "Backup Location Removal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }


        private void BackupBrowserMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            BackupDirectoryBrowser browserWindows = new BackupDirectoryBrowser();

            // if the selected backup is not null then show it in the backup directory browser
            if( model.SelectedBackup != null )
            {
                browserWindows.BackupDirectoryPicker.DirectoryPath = model.SelectedBackup.DestinationPath;
            }

            browserWindows.Show();
        }

        private void SaveNotesButton_Click(object sender, RoutedEventArgs e)
        {
            SaveNotes();
        }

        /**
         * <summary>
         *  This is called when the user hits the save notes button and it is also called if the user closes the form with notes being entered just incase they wheren't saved
         * </summary>
         */
        private void SaveNotes()
        {
            File.WriteAllText(NotesFilePath, NotesTextBox.Text);
            SaveNotesButton.Background = Brushes.Green;
        }


        /**
         * <summary>
         *   This will clear out the notes textbox and delete the notes.txt file if it exist
         *   
         *   It will also set the save notes button back to the default button background
         *   This function should be safely callable by a thread without causing an exception
         * </summary>
         */
        private void ClearNotes()
        {
            NotesTextBox.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                {
                    NotesTextBox.Text = string.Empty;
                }));
            

            if (File.Exists(NotesFilePath))
            {
                File.Delete(NotesFilePath);
            }


            // since we cleared it we want the save button's background to go back to grey
            SaveNotesButton.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                {
                    SaveNotesButton.Background = DefaultButtonBackGround;
                }));

            
        }

        

        private void ClearNotesButton_Click(object sender, RoutedEventArgs e)
        {
            ClearNotes();
        }

        /**
         * <summary>
         *  This indicates that since the notes changed they aren't saved anymore
         * </summary>
         */
        private void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveNotesButton.Background = Brushes.Red;

        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // when the log changes scroll to the end
            LogTextBox.ScrollToEnd();

            
        }

        private void CheckSpellingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // get the next spelling error in the text box if no error is found the textbox should not contain any mistakes
            int nextSpellingErrorCaretIndex = NotesTextBox.GetNextSpellingErrorCharacterIndex(0, LogicalDirection.Forward);
            // determine if any spelling errors where found
            if (nextSpellingErrorCaretIndex != -1)
            {
                // try to get a spelling error at the found location
                SpellingError spError = NotesTextBox.GetSpellingError(nextSpellingErrorCaretIndex);
                if (spError != null)
                {

                    // set the caretindex of the notes textbox to be where the error occured
                    NotesTextBox.CaretIndex = nextSpellingErrorCaretIndex;

                    // write out all the suggestions
                    SpellingHintsLabel.Content = spError.Suggestions.Aggregate(new StringBuilder(), (sb, suggestion) =>
                    {
                        return sb.AppendLine(suggestion);
                    }).ToString();

                    SpellingHintsExpander.IsExpanded = true;
                }
            }
            else
            {
                // no spelling errors where found
                SpellingHintsLabel.Content = string.Empty;
                SpellingHintsExpander.IsExpanded = false;
            }
        }// end of check spelling menu item



        private void LoadBackupSet()
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            try
            {
                // get the file path to load
                var fileDialog = new System.Windows.Forms.OpenFileDialog();
                var fileDialogResult = fileDialog.ShowDialog(NacWPFControls.MyWpfExtensions.GetIWin32Window(this));

                if (fileDialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    model.BackupSetPath = fileDialog.FileName;
                    ShowBusy(); // now that the user had made all the decisions show a busy indicator

                    Thread loadBackupTypesThread = new Thread((ThreadStart)delegate()
                    {
                        // load the backups from the file path
                        var backupTypeList = BackupType.LoadBackupTypeListFromConfigFile(model.BackupSetPath);

                        this.Dispatcher.Invoke(new Action(delegate()
                        {
                            model.LoadBackupTypes(backupTypeList);
                            HideBusy();
                        }));
                    });

                    loadBackupTypesThread.Start();
                }
            }
            catch (Exception ex)
            {
                LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                {
                    Foreground = Brushes.Red,
                    Text = "Problem occured while attemping to load backups from config file: {0}, Exception: {1}".With(model.BackupSetPath,
                                                                                                                         ex.Message)
                }));

                HideBusy();
            }
        }



        private void LoadBackupSetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;


            // if there are entries in the backups list ask the user if they want to save them before loading a backup set
            if (model.BackupTypes.Count > 0)
            {
                // prompt to make sure changes are saved
                var promptResult = System.Windows.MessageBox.Show("Please make sure that any changes to the current backup set have been saved.  To continue loading a new backup set click ok, this will clear out the currently loaded backup set.  Else click cancel.", "Have you saved any changes?", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (promptResult == MessageBoxResult.OK)
                {
                    LoadBackupSet();
                    return;
                }
            }
            else
            {
                LoadBackupSet(); // load a backup set without prompting since no backup types exist, meaning there is nothing that needs to be saved
            }


 
        }// end of load backup set menu item


        private void NewBackupSetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            // if there is stuff that might need to be saved prompt the user
            if (model.BackupTypes.Count > 0)
            {
                // prompt to make sure the user has saved everything they want to save
                var promptResult = System.Windows.MessageBox.Show("Please make sure that any changes to the current backup set have been saved.  To continue click ok and a new backup set will be created.  This will clear out the currently loaded backup set.  Else click cancel.", "Have you saved changes?", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (promptResult == MessageBoxResult.OK)
                {
                    model.ClearBackups();
                }
            }
        }

        /**
         * <summary>
         *   This does the actual saving of the backup set and is called by the save backup set menu item click
         * </summary>
         */
        private void SaveBackupSet(string path)
        {
            MainViewModel model = (MainViewModel)this.DataContext;
            try
            {
                model.SaveBackupTypes(path);

                LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                {
                    Foreground = Brushes.Blue,
                    Text = "All backups have been saved to the config file: {0}.".With(path)
                }));

            }
            catch (Exception ex)
            {
                LogTextBox.Document.AddParagraphs(new Paragraph(new Run
                {
                    Foreground = Brushes.Red,
                    Text = "Unable to save backups to config file: {0}.  Exception: {1}".With(path, ex.ToString())
                }));
            }
        }

        private void SaveBackupSetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;


            // if backup set path is not null ask the user if they want to overwrite the current backup set
            if (!string.IsNullOrEmpty(model.BackupSetPath))
            {
                var result = System.Windows.MessageBox.Show("By clicking ok you will overwrite the current backup location {0}.  If this is not what you want click cancel and you will be able to pick a new location to save the backup set to.".With(model.BackupSetPath), "Overwriting backup set config file", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.OK)
                {
                    // use the backup set path to save the backups to
                    SaveBackupSet(model.BackupSetPath);
                    return;
                }
            }

            // prompt the user to pick a place to save the backup set
            //http://www.java2s.com/Tutorial/VB/0260__GUI/SetOpenFileDialogFilterandgetselectedfilename.htm
            var fileDialog = new System.Windows.Forms.SaveFileDialog();
            fileDialog.Filter = "XML (*.xml) |*.xml";
            var fileDialogResult = fileDialog.ShowDialog(NacWPFControls.MyWpfExtensions.GetIWin32Window(this));
            if (fileDialogResult == System.Windows.Forms.DialogResult.OK)
            {
                model.BackupSetPath = fileDialog.FileName;
                SaveBackupSet(model.BackupSetPath);
            }

        }


        /**
         * <summary>
         *  Prompt the user to determine if they really wanted to exit the program
         * </summary>
         */
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            MainViewModel model = (MainViewModel)this.DataContext;

            // prompt user to determine if the current backup set is saved
            // it will only prompt if any backups exist in the model, if they dont' then there is nothing to save
            if (model.BackupTypes.Count > 0)
            {
                MessageBoxResult result;

                if (string.IsNullOrEmpty(model.BackupSetPath))
                {
                    result = System.Windows.MessageBox.Show("The current backups are not saved.  If you want to save them before closing click cancel.  If you want to close the window and loose all your data click ok", "!!!Warning!!! Backups not saved.", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                }
                else
                {
                    result = System.Windows.MessageBox.Show("If you have ensured that the backup set is saved, or you do not want to save it click ok.  If you are unsure wether the backupset is saved or not click cancel", "Are you sure you want to close?", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                }

                if (result == MessageBoxResult.Cancel)
                {
                    // cancel the close
                    e.Cancel = true;
                }
            }


            if (!string.IsNullOrEmpty(NotesTextBox.Text))
            {
                // save the notes
                SaveNotes();
            }

        }

        private void StopBackupButton_Click(object sender, RoutedEventArgs e)
        {
           
            this.Stop = true;
            Clear();
        }


        private void Clear()
        {
            MainViewModel model = (MainViewModel)this.DataContext;


            model.StopBackupTimer();
            // reset the counters in the model since we are doing a new backup
            model.ResetCounters();

            // Before starting the backup thread clear everything out
            LogTextBox.Document.Blocks.Clear();

            LogTextBox.Document.AddParagraphs(new Paragraph(new Run
            {
                Foreground = Brushes.Purple,
                Text = "Backup Cleared"
            }));
        }

    }// end of class



}// end of namespace
