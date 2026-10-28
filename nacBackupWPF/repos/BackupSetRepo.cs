using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using nacBackupWPF.models;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  Framework-agnostic work that backs the "Backup Sets" menu (Load / Save / New).
 *
 *  The user-interaction bits (the OpenFileDialog / SaveFileDialog and the
 *  "are you sure?" confirmation message boxes) differ between WPF and Avalonia,
 *  so those stay in the calling window's code-behind.  What this repository owns
 *  is everything both UIs must do the same way:
 *    + parse a config file into a BackupType list (on a background thread)
 *    + write a BackupType list to a config file
 *    + wrap a load so the busy indicator is shown while the work runs
 *
 *  The windows call these the same way:
 *    await setRepo.LoadFromAsync(path);
 *    setRepo.SaveBackupTypes(path);
 *    model.ClearBackups();   // "New"
 * </summary>
 */
public class BackupSetRepo
{
    private readonly MainViewModel model;
    private readonly UiContext ui;

    public BackupSetRepo(MainViewModel model, UiContext ui)
    {
        this.model = model;
        this.ui = ui;
    }

    /**
     * <summary>
     *  Load backup types from a config file.  The file is parsed off the UI
     *  thread and the results are pushed onto the model on the UI thread.  The
     *  busy indicator is shown for the whole operation.
     * </summary>
     */
    public async Task LoadFromAsync(string configFilePath)
    {
        model.IsBusy = true;
        try
        {
            List<BackupType> backupTypeList = await Task.Run(() =>
                BackupTypeRepository.LoadBackupTypes(configFilePath));

            ui.RunOnUi(() =>
            {
                model.LoadBackupTypes(backupTypeList);
                model.IsBusy = false;
            });
        }
        catch (Exception ex)
        {
            ui.RunOnUi(() =>
            {
                model.IsBusy = false;
                model.Log = string.IsNullOrEmpty(model.Log)
                    ? string.Format("Problem occured while attempting to load backups from config file: {0}, Exception: {1}",
                        model.BackupSetPath, ex.Message)
                    : model.Log + Environment.NewLine +
                      string.Format("Problem occured while attempting to load backups from config file: {0}, Exception: {1}",
                        model.BackupSetPath, ex.Message);
            });
        }
    }

    /**
     * <summary>
     *  This does the actual saving of the backup set to the given path.
     * </summary>
     */
    public void SaveBackupTypes(string path)
    {
        try
        {
            BackupTypeRepository.SaveBackupTypes(BackupTypeRepository.ToList(model.BackupTypes), path);

            model.Log = string.IsNullOrEmpty(model.Log)
                ? $"All backups have been saved to the config file: {path}."
                : model.Log + Environment.NewLine + $"All backups have been saved to the config file: {path}.";
        }
        catch (Exception ex)
        {
            model.Log = string.IsNullOrEmpty(model.Log)
                ? $"Unable to save backups to config file: {path}.  Exception: {ex}"
                : model.Log + Environment.NewLine + $"Unable to save backups to config file: {path}.  Exception: {ex}";
        }
    }
}
