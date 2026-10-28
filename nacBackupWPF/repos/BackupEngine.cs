using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using nacBackupWPF.models;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  The framework-agnostic backup engine.  This is the code that used to live in
 *  the WPF window's code-behind (creating a zip file of the backup locations,
 *  reporting progress, and then appending the log and notes into the archive).
 *
 *  Both the WPF UI and the Avalonia UI call the exact same engine.  Any UI work
 *  is marshalled onto the correct thread through the supplied UiContext, so this
 *  class never references WPF or Avalonia types directly.
 *
 *  The color/weight metadata that the old WPF RichTextBox carried on each log
 *  line is not preserved: the shared log is now a single plain-text string
 *  (model.Log).
 * </summary>
 */
public class BackupEngine
{
    private readonly MainViewModel model;
    private readonly UiContext ui;
    private readonly AppPaths app;

    public BackupEngine(MainViewModel model, UiContext ui, AppPaths app)
    {
        this.model = model;
        this.ui = ui;
        this.app = app;
    }

    /**
     * <summary>
     *  Begin a backup of the currently selected backup type.
     * </summary>
     */
    public async Task StartBackup()
    {
        model.Stop = false;

        await Clear();

        if (model.SelectedBackup != null)
        {
            // check a few things before gathering more information to perform the backup
            if (model.SelectedBackup.Name != null && model.SelectedBackup.DestinationPath != null && Directory.Exists(model.SelectedBackup.DestinationPath) && model.SelectedBackup.BackupLocations.Count >= 1)
            {

                model.StartBackupTimer();

                var validBackupLocations = (from backupLocation in model.SelectedBackup.BackupLocations
                                            where !string.IsNullOrEmpty(backupLocation.BackupLocationName) &&
                                                  !string.IsNullOrEmpty(backupLocation.BackupLocationPath) &&
                                                  Directory.Exists(backupLocation.BackupLocationPath)
                                            select backupLocation).ToList();

                string zipFileName = $@"{model.SelectedBackup.DestinationPath}\{model.SelectedBackup.Name}_{model.SelectedBackup.TimeStampValue}.zip";

                await AppendLog("Backup Filename: " + zipFileName);

                // capture the locals so the worker thread is not affected by later edits
                var backupLocations = validBackupLocations;

                // start a new thread to save the zip file
                Thread saveZipFileThread = new Thread((() =>
                {
                    try
                    {
                        using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile())
                        {
                            zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;
                            zip.CompressionMethod = Ionic.Zip.CompressionMethod.BZip2;

                            zip.SaveProgress += (sender, e) => OnZipSaveProgress(e);

                            // loop through the valid backups and add them to the zip file
                            foreach (var backupLocation in backupLocations)
                            {
                                zip.AddDirectory(backupLocation.BackupLocationPath, backupLocation.BackupLocationName);
                            }

                            zip.Save(zipFileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        ui.RunOnUi(() =>
                        {
                            AddLogToModel(string.Format("Error backing up {0}.  Exception message: {1}", model.SelectedBackup.Name, ex.Message));

                            // stop the backup timer since something went wrong with the backup
                            model.StopBackupTimer();
                        });
                    }

                    // put the log into an rtf entry and add it to the zip
                    // put the notes into a text entry and add it to the zip
                    SaveLogToZip(zipFileName);
                    SaveNotesToZip(zipFileName);
                }));

                saveZipFileThread.Start();
            }
            else // something is wrong with the backup type
            {
                var errorMessage = string.Empty;

                if (!Directory.Exists(model.SelectedBackup.DestinationPath))
                {
                    errorMessage += $"Directory {model.SelectedBackup.DestinationPath} does not exist";
                }

                if (model.SelectedBackup.BackupLocations.Count < 1)
                {
                    errorMessage += "There are no backup locations to backup";
                }

                if (model.SelectedBackup.Name == null || model.SelectedBackup.DestinationPath == null)
                {
                    errorMessage += $"Something is wrong with the backup name({model.SelectedBackup.Name}) or the destination path({model.SelectedBackup.DestinationPath})";
                }

                await AppendLog(errorMessage);
            }
        }
        else // nothing was selected
        {
            await AppendLog("You did not select a backup type from the list");
        }
    }

    /**
     * <summary>
     *  Ask a running backup to stop.  The next save-progress event will notice
     *  this and cancel the operation.
     * </summary>
     */
    public void Stop()
    {
        model.Stop = true;
    }

    /**
     * <summary>
     *  Reset the counters and write a "cleared" line to the log.
     * </summary>
     */
    public async Task Clear()
    {
        model.StopBackupTimer();
        // reset the counters since we are doing a new backup
        model.ResetCounters();

        // Before starting the backup thread clear everything out
        AddLogToModel(null);

        await AppendLog("Backup Cleared");
    }

    /// <summary>
    ///  Handle a single SaveProgress event from the underlying zip save.  The
    ///  event is raised on the zip worker thread, so all UI updates are
    ///  marshalled through the UiContext back onto the UI thread.
    /// </summary>
    private void OnZipSaveProgress(Ionic.Zip.SaveProgressEventArgs e)
    {
        if (model.Stop == true)
        {
            e.Cancel = true;
            return;
        }

        if (e.EventType == Ionic.Zip.ZipProgressEventType.Saving_AfterWriteEntry)
        {
            // set the backup progress bar value
            ui.RunOnUi(() =>
            {
                model.BackupProgressPercent = (int)(((double)e.EntriesSaved / (double)e.EntriesTotal) * 100);
            });

            if (e.CurrentEntry.IsDirectory == true)
            {
                ui.RunOnUi(() =>
                {
                    AddLogToModel("Backing up Directory: " + e.CurrentEntry.FileName);

                    // increment the Directory count
                    model.DirectoryCount += 1;
                });
            }
            else // it is a file instead of a directory
            {
                // write out the file name
                ui.RunOnUi(() =>
                {
                    AddLogToModel($"\t\t{e.CurrentEntry.FileName}");

                    // increment the file count
                    model.FileCount += 1;

                    // increment the sizes for the file
                    model.CompressedBytesCounter += e.CurrentEntry.CompressedSize;
                    model.UnCompressedBytesCounter += e.CurrentEntry.UncompressedSize;
                });
            }
        }
        else if (e.EventType == Ionic.Zip.ZipProgressEventType.Saving_Completed)
        {
            ui.RunOnUi(() =>
            {
                // update the progress percent
                model.BackupProgressPercent = 100;

                // write out a message that the backup succeeded
                AddLogToModel("Backup Is Now Complete !!!");

                // we are done so stop the backup timer
                model.StopBackupTimer();

                // write out the time it took to perform the backup
                AddLogToModel(string.Format("Backup completed in {0}", model.BackupTimeElapsedStr));

                // the compressed size, uncompressed size, and compression ratio should be appended onto the end of the log
                AddLogToModel(string.Format("Compressed Size: {0}\tUnCompressed Size: {1}\tCompression Ratio: {2}",
                    model.CompressedBytesCounterStr,
                    model.UnCompressedBytesCounterStr,
                    model.CompressionRatioStr));
            });
        }
    }

    /**
     * <summary>
     *   Assumption that this is running on the zip save thread.
     * </summary>
     */
    private void SaveLogToZip(string zipFileName)
    {
        try
        {
            var currentLog = string.Empty;
            ui.RunOnUi(() =>
            {
                currentLog = model.Log ?? string.Empty;

                File.WriteAllText(app.LogFilePath, currentLog);
            });

            using (Ionic.Zip.ZipFile zip = Ionic.Zip.ZipFile.Read(zipFileName))
            {
                zip.AddFile(app.LogFilePath, "");
                zip.Save();
            }

            File.Delete(app.LogFilePath);
        }
        catch (Exception ex)
        {
            ui.RunOnUi(() =>
            {
                AddLogToModel(string.Format("Error Saving log to {0}.  Exception message: {1}", zipFileName, ex.Message));
            });
        }
    }

    /**
     * <summary>
     *  Assumption that this is running on the zip save thread.
     */
    private void SaveNotesToZip(string zipFileName)
    {
        try
        {
            var currentNotes = string.Empty;
            ui.RunOnUi(() =>
            {
                currentNotes = model.Notes ?? string.Empty;

                File.WriteAllText(app.NotesFilePath, currentNotes);
            });

            using (Ionic.Zip.ZipFile zip = Ionic.Zip.ZipFile.Read(zipFileName))
            {
                zip.AddFile(app.NotesFilePath, "");
                zip.Save();
            }

            // clear the notes after they have been packed into the archive
            ui.RunOnUi(() =>
            {
                model.Notes = string.Empty;
            });
        }
        catch (Exception ex)
        {
            ui.RunOnUi(() =>
            {
                AddLogToModel(string.Format("Error Saving notes to {0}.  Exception message: {1}", zipFileName, ex.Message));
            });
        }
    }

    // ----- log helpers --------------------------------------------------------

    /// <summary>
    ///  Append a line to the log and notify the UI.  This is the UI-thread
    ///  operation that both the WPF and Avalonia UIs end up calling.
    /// </summary>
    private void AddLogToModel(string line)
    {
        string current = model.Log;

        model.Log = string.IsNullOrEmpty(current)
            ? line
            : current + Environment.NewLine + line;
    }

    /// <summary>
    ///  Convenience wrapper that marshals AddLogToModel onto the UI thread for
    ///  callers that are not already on it.
    /// </summary>
    private async Task AppendLog(string text)
    {
        await ui.RunOnUiAsync(async () =>
        {
            AddLogToModel(text);
        });
    }
}
