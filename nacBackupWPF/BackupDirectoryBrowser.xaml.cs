using System;
using System.IO;
using System.Windows;

using nacBackupWPF.models;
using nacBackupWPF.repos;

namespace nacBackupWPF
{
    /// <summary>
    /// Interaction logic for BackupDirectoryBrowser.xaml
    ///
    /// This is the WPF UI for the backup directory browser.  All of the directory
    /// scanning / notes reading / open-the-file work has been moved into the
    /// framework-agnostic BackupBrowserRepo under the nacBackupWPF.repos namespace
    /// so that the Avalonia UI can call exactly the same code.  What remains here
    /// is WPF-specific: the window and the busy indicator.
    /// </summary>
    public partial class BackupDirectoryBrowser : Window
    {
        private int numberOfBusy = 0;

        private BackupDirectoryViewModel model;
        private WpfUiContext ui;
        private AppPaths app;
        private BackupBrowserRepo browserRepo;

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

            model = (BackupDirectoryViewModel)this.DataContext;

            // The same framework-agnostic repositories the Avalonia UI uses.
            app = new AppPaths();
            ui = new WpfUiContext(Dispatcher);
            browserRepo = new BackupBrowserRepo(model, ui, app);
        }

        private void BackupDirectoryPicker_DirectoryPathChanged(object sender, EventArgs e)
        {
            if (Directory.Exists(BackupDirectoryPicker.DirectoryPath))
            {
                ShowBusy();
                browserRepo.PopulateDirectoryEntries(BackupDirectoryPicker.DirectoryPath);
            }
        }

        private void OpenBackupEntryButton_Click(object sender, RoutedEventArgs e)
        {
            BackupDirectoryEntry entry = (BackupDirectoryEntry)((FrameworkElement)sender).DataContext;

            browserRepo.OpenBackupEntry(entry);
        }
    }
}
