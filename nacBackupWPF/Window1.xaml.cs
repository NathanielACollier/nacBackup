using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using nacBackupWPF.models;
using nacBackupWPF.repos;
using nac.wpf.Extensions;

namespace nacBackupWPF
{

    /// <summary>
    /// Interaction logic for Window1.xaml
    ///
    /// This is the WPF UI.  All of the backup / loading / saving / notes work has
    /// been moved into repositories under the nacBackupWPF.repos namespace so that
    /// the Avalonia UI (Window1_Avalonia.cs) can call exactly the same code.  What
    /// remains here is WPF-specific: the window, the message boxes and file
    /// dialogs, and the WPF control bindings.
    /// </summary>
    public partial class Window1 : Window
    {
        private MainViewModel model;
        private WpfUiContext ui;
        private AppPaths app;

        private NotesRepo notesRepo;
        private BackupSetRepo setRepo;
        private BackupEngine engine;
        private ElapseTimer elapseTimer;

        public Brush DefaultButtonBackGround
        {
            get { return (new Button()).Background; }
        }


        public Window1()
        {
            InitializeComponent();

            // The XAML creates the model as the window's DataContext.
            model = (MainViewModel)this.DataContext;

            // Wire up the framework-agnostic repositories that the Avalonia UI
            // uses the exact same way.
            app = new AppPaths();
            ui = new WpfUiContext(Dispatcher);
            notesRepo = new NotesRepo(model, app, ui);
            setRepo = new BackupSetRepo(model, ui);
            engine = new BackupEngine(model, ui, app);
            elapseTimer = new ElapseTimer(model, ui);

            // Load any saved notes and start the elapsed-time ticker.
            notesRepo.PopulateNotesFromNotesFile();
            elapseTimer.Start();

            //*** no longer automatically loading a backup set when the application launches

            // Seed the log with an opening message.
            model.Log = "Application Started.  Now logging.  Write log information here.";

            // The log lives in the shared model.Log string (the backup engine
            // appends to it).  A WPF RichTextBox has no Text binding, so mirror
            // the log text into the document as the model changes.
            model.PropertyChanged += OnModelLogChanged;
        }

        private void OnModelLogChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.Log))
            {
                var textRange = new TextRange(LogTextBox.Document.ContentStart, LogTextBox.Document.ContentEnd);
                textRange.Text = model.Log;

                LogTextBox.ScrollToEnd();
            }
        }


        private void NewBackupButton_Click(object sender, RoutedEventArgs e)
        {
            model.BackupTypes.Add(new BackupType());

            model.SelectedBackup = model.BackupTypes.Last(); // should select the one we just added
        }

        /**
         * <summary>
         *  This removes the backup that is selected by the drop down from the
         *  backup list then re displays the form
         * </summary>
         */
        private void RemoveBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (model.SelectedBackup != null)
            {
                model.BackupTypes.Remove(model.SelectedBackup);

                model.SelectedBackup = null;
            }
        }


        /**
         * <summary>
         *  Begin a backup.  The actual work lives in the shared BackupEngine so
         *  the Avalonia UI calls the same code.
         * </summary>
         */
        private async void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            await engine.StartBackup();
        }


        private void AddBackupLocationButton_Click(object sender, RoutedEventArgs e)
        {
            if (model.SelectedBackup != null)
            {
                model.SelectedBackup.BackupLocations.Add(new models.BackupLocationType());
            }
        }

        private void RemoveBackupLocationButton_Click(object sender, RoutedEventArgs e)
        {
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
            BackupDirectoryBrowser browserWindows = new BackupDirectoryBrowser();

            // if the selected backup is not null then show it in the backup directory browser
            if (model.SelectedBackup != null)
            {
                browserWindows.BackupDirectoryPicker.DirectoryPath = model.SelectedBackup.DestinationPath;
            }

            browserWindows.Show();
        }

        private void SaveNotesButton_Click(object sender, RoutedEventArgs e)
        {
            notesRepo.SaveNotes();

            // visual cue that the notes have been saved
            SaveNotesButton.Background = Brushes.Green;
        }


        /**
         * <summary>
         *   This is called when the user hits the clear notes button.
         * </summary>
         */
        private void ClearNotesButton_Click(object sender, RoutedEventArgs e)
        {
            notesRepo.ClearNotes();

            // since we cleared it we want the save button's background to go back to grey
            SaveNotesButton.Background = DefaultButtonBackGround;
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


        /**
         * <summary>
         *  Load a backup set.  The confirmation prompt and the open-file dialog
         *  are WPF-specific and stay here; the actual load (parse the config file
         *  on a background thread and push the results onto the model) lives in
         *  BackupSetRepo.LoadFromAsync, which the Avalonia UI calls the same way.
         * </summary>
         */
        private async void LoadBackupSetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // if there are entries in the backups list ask the user if they want
            // to save them before loading a backup set
            if (model.BackupTypes.Count > 0)
            {
                // prompt to make sure changes are saved
                var promptResult = System.Windows.MessageBox.Show("Please make sure that any changes to the current backup set have been saved.  To continue loading a new backup set click ok, this will clear out the currently loaded backup set.  Else click cancel.", "Have you saved any changes?", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (promptResult == MessageBoxResult.OK)
                {
                    await RunLoadBackupSet();
                    return;
                }
            }
            else
            {
                await RunLoadBackupSet(); // load a backup set without prompting since no backup types exist
            }
        }// end of load backup set menu item


        private async Task RunLoadBackupSet()
        {
            try
            {
                // get the file path to load
                var fileDialog = new System.Windows.Forms.OpenFileDialog();
                var fileDialogResult = fileDialog.ShowDialog(owner: Win32Extensions.GetIWin32Window(this));

                if (fileDialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    model.BackupSetPath = fileDialog.FileName;

                    // parse the config file off the UI thread and push the
                    // results onto the model (the repo shows the busy indicator)
                    await setRepo.LoadFromAsync(model.BackupSetPath);
                }
            }
            catch (Exception ex)
            {
                model.Log = AppendLogLine(model.Log,
                    $"Problem occured while attemping to load backups from config file: {model.BackupSetPath}, Exception: {ex.Message}");
            }
        }


        private void NewBackupSetMenuItem_Click(object sender, RoutedEventArgs e)
        {
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
         *  Save the backup set.  The overwrite confirmation and the save-file
         *  dialog are WPF-specific and stay here; the actual write (and the log
         *  message) lives in BackupSetRepo.SaveBackupTypes.
         * </summary>
         */
        private void SaveBackupSetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // if backup set path is not null ask the user if they want to overwrite the current backup set
            if (!string.IsNullOrEmpty(model.BackupSetPath))
            {
                var result = System.Windows.MessageBox.Show($"By clicking ok you will overwrite the current backup location {model.BackupSetPath}.  If this is not what you want click cancel and you will be able to pick a new location to save the backup set to.", "Overwriting backup set config file", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.OK)
                {
                    // use the backup set path to save the backups to
                    setRepo.SaveBackupTypes(model.BackupSetPath);
                    return;
                }
            }

            // prompt the user to pick a place to save the backup set
            var fileDialog = new System.Windows.Forms.SaveFileDialog();
            fileDialog.Filter = "XML (*.xml) |*.xml";
            var fileDialogResult = fileDialog.ShowDialog(owner: Win32Extensions.GetIWin32Window(this));
            if (fileDialogResult == System.Windows.Forms.DialogResult.OK)
            {
                model.BackupSetPath = fileDialog.FileName;
                setRepo.SaveBackupTypes(model.BackupSetPath);
            }
        }


        /**
         * <summary>
         *  Prompt the user to determine if they really wanted to exit the program,
         *  then save the notes if any are entered.
         * </summary>
         */
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // prompt user to determine if the current backup set is saved
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
                // save the notes just in case they weren't saved
                notesRepo.SaveNotes();
            }
        }

        private void StopBackupButton_Click(object sender, RoutedEventArgs e)
        {
            engine.Stop();
            _ = engine.Clear();
        }




        /**
         * <summary>
         *  Shared helper used by both the window and the engine to append a line
         *  to the plain-text log in model.Log.
         * </summary>
         */
        private string AppendLogLine(string current, string line)
        {
            return string.IsNullOrEmpty(current) ? line : current + Environment.NewLine + line;
        }

    }// end of class

}
