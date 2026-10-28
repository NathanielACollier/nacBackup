using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;
using nacBackupWPF.models;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  Framework-agnostic logic for the "Backup Directory Browser" window.
 *
 *  This is the work that used to live in BackupDirectoryBrowser.xaml.cs: given a
 *  directory, scan it for zip backup files on a background thread, read the
 *  notes out of each archive, and add a BackupDirectoryEntry to the model per
 *  file.  Any UI update is marshalled to the UI thread via the UiContext, so the
 *  WPF and Avalonia windows can both drive the exact same scan.
 * </summary>
 */
public class BackupBrowserRepo
{
    private const string BackupFileNamePattern =
        @"(?<BackupName>.*)_(?<DateTime>\d+.\d+.\d+_\d+.\d+.\d+)(?<PostCode>\w+)(?<Extension>\.\w+)";

    private readonly BackupDirectoryViewModel model;
    private readonly UiContext ui;
    private readonly AppPaths app;

    public BackupBrowserRepo(BackupDirectoryViewModel model, UiContext ui, AppPaths app)
    {
        this.model = model;
        this.ui = ui;
        this.app = app;
    }

    /**
     * <summary>
     *  Scan a directory for zip backup files and populate the model.
     * </summary>
     */
    public void PopulateDirectoryEntries(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        model.IsBusy = true;

        Thread getDirectoryEntriesThread = new Thread(() =>
        {
            // do a query to get all zip files in the directory
            var zipFilesQuery = from fileInfo in Directory.GetFiles(directoryPath)
                                       .Select(file => new FileInfo(file))
                                   where string.Equals(fileInfo.Extension, ".zip", StringComparison.OrdinalIgnoreCase)
                                   orderby fileInfo.LastWriteTime descending
                                   let match = Regex.Match(fileInfo.Name, BackupFileNamePattern)
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
                string notes = ReadNotes(entry.FileInformation.FullName);

                ui.RunOnUi(() =>
                {
                    model.BackupEntries.Add(new BackupDirectoryEntry
                    {
                        Path = entry.FileInformation.FullName,
                        Notes = notes,
                        FileSize = nacBackupWPF.lib.ByteSize.BytesToString(entry.FileInformation.Length),
                        FileSizeBytes = entry.FileInformation.Length,
                        BackupName = entry.BackupName
                    });

                    // refresh everything as we go along
                    model.RefreshBackupEntryes();
                    // set the type to be viewed to be the current type
                    model.SelectedBackupName = entry.BackupName;
                });
            }

            ui.RunOnUi(() =>
            {
                model.IsBusy = false;
            });
        });

        getDirectoryEntriesThread.Start();
    }

    /**
     * <summary>
     *  Open the zip file for a backup entry with the default application.
     * </summary>
     */
    public void OpenBackupEntry(BackupDirectoryEntry entry)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = entry.Path,
            UseShellExecute = true
        });
    }

    /**
     * <summary>
     *  Read the notes.txt entry out of a zip archive, if present.
     * </summary>
     */
    private string ReadNotes(string zipFilePath)
    {
        string notes = string.Empty;

        using (Ionic.Zip.ZipFile zip = Ionic.Zip.ZipFile.Read(zipFilePath))
        {
            try
            {
                using (System.IO.Stream reader = zip["notes.txt"].OpenReader())
                {
                    byte[] data = new byte[reader.Length];
                    reader.Read(data, 0, (int)reader.Length);
                    notes = Encoding.ASCII.GetString(data);
                }
            }
            catch (Exception ex)
            {
                // ignore - the zip might not have a notes.txt
            }
        }

        return notes;
    }
}
