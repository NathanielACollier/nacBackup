using System;
using System.Threading.Tasks;
using nac.Forms;
using nacBackupWPF.models;
using nacBackupWPF.repos;

namespace nacBackupWPF;

/**
 * <summary>
 *  The Avalonia / nac.Forms equivalent of BackupDirectoryBrowser.  It is shown
 *  from the main form with DisplayChildForm.  All of the directory scanning /
 *  notes reading / open-the-file work lives in the shared BackupBrowserRepo so
 *  the WPF and Avalonia browsers do the identical thing.  The only Avalonia
 *  specific bit here is the layout, built with the nac.Forms fluent builder.
 * </summary>
 */
public class BackupDirectoryBrowser_Avalonia
{
    private BackupDirectoryViewModel model;
    private BackupBrowserRepo repo;

    public BackupDirectoryBrowser_Avalonia(MainViewModel mainModel, UiContext ui, AppPaths app)
    {
        this.model = new BackupDirectoryViewModel();

        // pre-fill the directory with the currently selected backup's destination
        this.model.DirectoryPath = mainModel?.SelectedBackup?.DestinationPath;

        this.repo = new BackupBrowserRepo(this.model, ui, app);
    }

    public void BuildInto(Form f)
    {
        f.Title = "BackupDirectoryBrowser";
        f.DataContext = this.model;

        // Top row: the directory picker, the backup-name drop down, a busy hint.
        f.HorizontalGroup(h =>
        {
            h.Text("Backup Directory", style: "font-weight:bold;font-size:12;");
            h.DirectoryPathFor("DirectoryPath", onDirectoryPathChanged: async path =>
                {
                    await Task.CompletedTask;
                    if (System.IO.Directory.Exists(path))
                    {
                        this.repo.PopulateDirectoryEntries(path);
                    }
                });
            h.DropDown<string>(
                "BackupNames",
                "SelectedBackupName",
                style: "width:200px;");
        });

        // Summary row.
        f.HorizontalGroup(h =>
        {
            h.Text("Contents", style: "font-weight:bold;");
            h.HorizontalGroup(g =>
            {
                g.Text("Backup Type Size: ");
                g.TextFor("SelectedBackupTypeTotalSize", style: "font-weight:bold;");
            });
            h.HorizontalGroup(g =>
            {
                g.Text("Total Size: ");
                g.TextFor("TotalBackupsSize", style: "font-weight:bold;");
            });
        });

        // The list of backup entries.  Each row is a BackupDirectoryEntry.
        f.List<BackupDirectoryEntry>(
            "EntriesToDisplay",
            populateItemRow: row =>
            {
                row.HorizontalGroup(g =>
                {
                    g.Button("Open", async () =>
                    {
                        var entry = row.DataContext as BackupDirectoryEntry;
                        if (entry != null)
                        {
                            this.repo.OpenBackupEntry(entry);
                        }
                    });

                    g.Text("Path", style: "background:blue;color:white;font-weight:bold;");
                    g.TextFor("Path", style: "width:200px;");

                    g.Text("File Size", style: "background:blue;color:white;font-weight:bold;margin:10px 0 0 0;");
                    g.TextFor("FileSize");

                    g.Text("Notes", style: "background:blue;color:white;font-weight:bold;margin:10px 0 0 0;");
                    g.TextFor("Notes", style: "width:500px;");
                });
            });

        // Scan the pre-filled directory if it exists.
        if (!string.IsNullOrEmpty(this.model.DirectoryPath) && System.IO.Directory.Exists(this.model.DirectoryPath))
        {
            this.repo.PopulateDirectoryEntries(this.model.DirectoryPath);
        }
    }
}
