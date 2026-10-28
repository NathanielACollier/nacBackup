using System;
using System.Linq;
using System.Threading.Tasks;
using nac.Forms;
using nacBackupWPF.models;
using nacBackupWPF.repos;
using MenuItem = nac.Forms.model.MenuItem;
using Column = nac.Forms.model.Column;

namespace nacBackupWPF;

/**
 * <summary>
 *  The Avalonia / nac.Forms equivalent of Window1.  It builds the whole UI in
 *  code with the nac.Forms fluent builder and wires every action to the exact
 *  same framework-agnostic repositories that the WPF Window1 uses:
 *
 *      BackupSetRepo     (load / save / new backup sets)
 *      BackupEngine      (the zip backup + progress)
 *      ElapseTimer       (the backup elapsed-time ticker)
 *      NotesRepo         (the notes file)
 *      AppPaths          (the notes / log file paths)
 *
 *  nac.Forms has no built-in message box, so the confirmation prompts use a
 *  child-form dialog (ShowConfirm).  The config-file open/save dialogs use the
 *  same System.Windows.Forms dialogs the WPF window uses; the actual load/save
 *  work stays in BackupSetRepo so both UIs do the identical thing.
 * </summary>
 */
public class Window1_Avalonia
{
    private nac.Forms.Form mainForm;

    private MainViewModel model;

    private AppPaths app;
    private AvaloniaUiContext ui;

    private NotesRepo notesRepo;
    private BackupSetRepo setRepo;
    private BackupEngine engine;
    private ElapseTimer elapseTimer;

    public Window1_Avalonia()
    {
    }

    public void Run()
    {
        this.mainForm = nac.Forms.Form.NewForm(config =>
        {
            config.AfterSetup(_ =>
            {
                // force light theme
                Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            });
        });

        this.model = new MainViewModel();
        this.mainForm.DataContext = this.model;

        // The same framework-agnostic repositories the WPF window uses.
        this.app = new AppPaths();
        this.ui = new AvaloniaUiContext();
        this.notesRepo = new NotesRepo(this.model, this.app, this.ui);
        this.setRepo = new BackupSetRepo(this.model, this.ui);
        this.engine = new BackupEngine(this.model, this.ui, this.app);
        this.elapseTimer = new ElapseTimer(this.model, this.ui);

        // Load any saved notes and start the elapsed-time ticker.
        this.notesRepo.PopulateNotesFromNotesFile();
        this.elapseTimer.Start();

        // Seed the log with an opening message.
        this.model.Log = "Application Started.  Now logging.  Write log information here.";

        buildForm(mainForm);
        mainForm.Display(onDisplay: onFormDisplay, onClosing: onFormClosing);
    }

    private void buildForm(nac.Forms.Form f)
    {
        f.Title = "Nathaniel Collier Backup Utility";

        // ----- menu ---------------------------------------------------------
        f.Menu(new[]
        {
            new MenuItem
            {
                Header = "File",
                Items = new[]
                {
                    new MenuItem { Header = "Backup Browser", Action = () => _ = OpenBackupBrowser() }
                }
            },
            new MenuItem
            {
                Header = "Tools",
                Items = new[]
                {
                    new MenuItem { Header = "Check Spelling", Action = () => AppendLog("Spelling check is not available in the Avalonia build.") }
                }
            },
            new MenuItem
            {
                Header = "Backup Sets",
                Items = new[]
                {
                    new MenuItem { Header = "Load", Action = () => _ = LoadBackupSet() },
                    new MenuItem { Header = "Save", Action = () => _ = SaveBackupSet() },
                    new MenuItem { Header = "New", Action = () => _ = NewBackupSet() }
                }
            }
        });

        // First Row: backup set path
        f.HorizontalGroup(h =>
        {
            h.Text("nacBackup - ")
             .Text(nacBackupWPF.lib.settings.appVersion, style: "color:blue;");
        });

        f.HorizontalGroup(h =>
        {
            h.Text("Backup Set Path:");
            h.TextFor("BackupSetPath", style: "width:700px;");
        });

        // Second Row: backup type dropdown + New/Remove
        f.HorizontalGroup(h =>
        {
            h.Text("Backup Type:");
            h.DropDown<BackupType>(
                "BackupTypes",
                "SelectedBackup",
                style: "width:500px;",
                populateItemRow: item =>
                {
                    item.HorizontalGroup(g =>
                    {
                        g.Text("Name:", style: "color:blue;font-weight:bold;");
                        g.TextFor("Name", style: "width:200px;");
                        g.Text("Destination Directory:", style: "color:blue;font-weight:bold;");
                        g.TextFor("DestinationPath", style: "width:300px;");
                    });
                });
            h.Button("New", async () => await NewBackup());
            h.Button("Remove", async () => await RemoveBackup());
        });

        // Backup Name
        f.HorizontalGroup(h =>
        {
            h.Text("Backup Name:");
            h.TextBoxFor("SelectedBackup.Name", style: "width:351px;");
        });

        // Third Row: time stamp format + value
        f.HorizontalGroup(h =>
        {
            h.Text("TimeStamp Format");
            h.TextBoxFor("SelectedBackup.TimeStampFormat", style: "width:277px;");
            h.Text("TimeStamp Value", style: "margin:30px 0 0 0;");
            h.TextBoxFor("SelectedBackup.TimeStampValue", isReadOnly: true, style: "width:277px;");
        });

        // Backup directory picker
        f.HorizontalGroup(h =>
        {
            h.Text("Backup Directory");
            h.DirectoryPathFor("SelectedBackup.DestinationPath", onDirectoryPathChanged: async path =>
                {
                    await Task.CompletedTask;
                });
            h.TextFor("SelectedBackup.DestinationPathSize", style: "margin:10px 0 0 0;");
        });

        // Add / remove backup location
        f.HorizontalGroup(h =>
        {
            h.Button("Add Backup Location", async () => await AddBackupLocation());
            h.Button("Remove Backup Location", async () => await RemoveBackupLocation());
        });

        // The backup locations table.  Each row is a BackupLocationType, so the
        // cell templates bind to that row's own properties (the same thing the
        // WPF GridView CellTemplates did).
        f.Table<BackupLocationType>(
            "SelectedBackup.BackupLocations",
            columns: new Column[]
            {
                new Column
                {
                    Header = "Path",
                    template = row =>
                    {
                        row.DirectoryPathFor("BackupLocationPath");
                    }
                },
                new Column
                {
                    Header = "Name",
                    template = row => row.TextBoxFor("BackupLocationName", style: "width:200px;")
                },
                new Column
                {
                    Header = "Size",
                    template = row => row.TextFor("BackupLocationSize", style: "width:150px;")
                },
                new Column
                {
                    Header = "Characters in path",
                    template = row => row.TextFor("BackupLocationPath.Length")
                }
            });

        // Fifth Row: counts + progress
        f.HorizontalGroup(h =>
        {
            h.Text("Files");
            h.TextFor("FileCount", style: "width:50px;background:black;color:red;");
            h.Text("Directories", style: "margin:40px 0 0 0;");
            h.TextFor("DirectoryCount", style: "width:50px;background:black;color:white;");
            h.Text("Backup Progress", style: "margin:20px 0 0 0;");
            h.Progress("BackupProgressPercent", 0, 100);
        });

        // Notes block
        f.VerticalGroup(v =>
        {
            v.HorizontalGroup(h =>
            {
                h.Text("Notes", style: "font-weight:bold;font-size:14;");
                h.Button("Save", async () => await SaveNotes());
                h.Button("Clear", async () => await ClearNotes());
                h.HorizontalGroup(c =>
                {
                    c.Text("Character Count ");
                    c.TextFor("Notes.Length", style: "font-weight:bold;");
                });
            });

            // Spelling suggestions had a WPF Expander; there is no equivalent in
            // nac.Forms so the label is left empty / not shown.
            v.TextBoxFor("Notes", multiline: true, style: "height:100px;");
        });

        // The log (was a WPF RichTextBox; now a plain multi-line text box).
        f.HorizontalGroup(h =>
        {
            h.TextBoxFor("Log", multiline: true, style: "width:990px;height:150px;");
        });

        // Seventh Row: size summary on the left, Stop/Backup on the right.
        f.VerticalDock(d =>
        {
            d.VerticalStack(verts =>
            {
                verts.HorizontalGroup(h =>
                {
                    h.Text("Compressed Size:");
                    h.TextFor("CompressedBytesCounterStr", style: "width:100px;background:white;color:blue;");
                    h.Text("UnCompressed Size:", style: "margin:20px 0 0 0;");
                    h.TextFor("UnCompressedBytesCounterStr", style: "width:100px;background:white;color:blue;");
                    h.Text("Compression Ratio:", style: "margin:20px 0 0 0;");
                    h.TextFor("CompressionRatioStr", style: "width:100px;background:white;color:blue;");
                });

                verts.HorizontalGroup(h =>
                {
                    h.Text("Backup Time Elapsed: ");
                    h.TextFor("BackupTimeElapsedStr", style: "color:blue;font-weight:bold;margin:10px 0 0 0;");
                });
            });

            d.VerticalStack(stops =>
            {
                stops.Button("Stop", async () => await StopBackup());
                stops.Button("Backup", async () => await StartBackup());
            });
        });
    }

    private async Task onFormDisplay(Form arg)
    {
    }

    private async Task<bool?> onFormClosing(Form arg)
    {
        bool? cancelClose = await AskIfBackupSetShouldBeSaved();
        if (cancelClose == true)
        {
            // returning true cancels the close
            return true;
        }

        // save the notes just in case they weren't saved
        this.notesRepo.SaveNotes();

        return false;
    }

    // ----- button / menu handlers.  The work lives in the shared repos -------

    private async Task NewBackup()
    {
        this.model.BackupTypes.Add(new BackupType());
        this.model.SelectedBackup = this.model.BackupTypes.Last();
    }

    private async Task RemoveBackup()
    {
        if (this.model.SelectedBackup != null)
        {
            this.model.BackupTypes.Remove(this.model.SelectedBackup);
            this.model.SelectedBackup = null;
        }
    }

    private async Task StartBackup()
    {
        await this.engine.StartBackup();
    }

    private async Task StopBackup()
    {
        this.engine.Stop();
        await this.engine.Clear();
    }

    private async Task AddBackupLocation()
    {
        if (this.model.SelectedBackup != null)
        {
            this.model.SelectedBackup.BackupLocations.Add(new BackupLocationType());
        }
    }

    private async Task RemoveBackupLocation()
    {
        if (this.model.SelectedBackup != null && this.model.SelectedBackupLocation != null)
        {
            this.model.SelectedBackup.BackupLocations.Remove(this.model.SelectedBackupLocation);
            this.model.SelectedBackupLocation = null;
        }
        else
        {
            await ShowConfirm(
                "Error removing backup location.  Either a backup or a location is not selected.",
                "Backup Location Removal Error");
        }
    }

    private Task SaveNotes()
    {
        this.notesRepo.SaveNotes();
        return Task.CompletedTask;
    }

    private Task ClearNotes()
    {
        this.notesRepo.ClearNotes();
        return Task.CompletedTask;
    }

    // ----- backup browser (a child form, see BackupDirectoryBrowser_Avalonia) -

    private async Task OpenBackupBrowser()
    {
        var browser = new BackupDirectoryBrowser_Avalonia(this.model, this.ui, this.app);

        await mainForm.DisplayChildForm(child =>
        {
            browser.BuildInto(child);
        }, height: 686, width: 978);
    }

    // ----- backup sets menu (Load / Save / New) -----------------------------

    private async Task LoadBackupSet()
    {
        if (this.model.BackupTypes.Count > 0)
        {
            var go = await ShowConfirm(
                "Please make sure that any changes to the current backup set have been saved.  To continue loading a new backup set click ok, this will clear out the currently loaded backup set.  Else click cancel.",
                "Have you saved any changes?");
            if (go)
            {
                await RunLoadBackupSet();
                return;
            }
        }
        else
        {
            await RunLoadBackupSet();
        }
    }

    private async Task RunLoadBackupSet()
    {
        try
        {
            // The open dialog is UI-specific.  Once we have the path the work is
            // done by the shared BackupSetRepo, exactly like the WPF window.
            using (var fileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                var fileDialogResult = fileDialog.ShowDialog();

                if (fileDialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    this.model.BackupSetPath = fileDialog.FileName;

                    // shared load logic (Busy indicator + parse on a background thread)
                    await this.setRepo.LoadFromAsync(this.model.BackupSetPath);
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Problem occured while attemping to load backups from config file: {this.model.BackupSetPath}, Exception: {ex.Message}");
        }
    }

    private async Task NewBackupSet()
    {
        if (this.model.BackupTypes.Count > 0)
        {
            var go = await ShowConfirm(
                "Please make sure that any changes to the current backup set have been saved.  To continue click ok and a new backup set will be created.  This will clear out the currently loaded backup set.  Else click cancel.",
                "Have you saved changes?");
            if (go)
            {
                this.model.ClearBackups();
            }
        }
    }

    private async Task SaveBackupSet()
    {
        if (!string.IsNullOrEmpty(this.model.BackupSetPath))
        {
            var ok = await ShowConfirm(
                $"By clicking ok you will overwrite the current backup location {this.model.BackupSetPath}.  If this is not what you want click cancel and you will be able to pick a new location to save the backup set to.",
                "Overwriting backup set config file");
            if (ok)
            {
                this.setRepo.SaveBackupTypes(this.model.BackupSetPath);
                return;
            }
        }

        // prompt the user to pick a place to save the backup set
        using (var fileDialog = new System.Windows.Forms.SaveFileDialog())
        {
            fileDialog.Filter = "XML (*.xml) |*.xml";
            var fileDialogResult = fileDialog.ShowDialog();
            if (fileDialogResult == System.Windows.Forms.DialogResult.OK)
            {
                this.model.BackupSetPath = fileDialog.FileName;
                this.setRepo.SaveBackupTypes(this.model.BackupSetPath);
            }
        }
    }

    // ----- closing prompt (was a WPF MessageBox) ----------------------------

    private async Task<bool?> AskIfBackupSetShouldBeSaved()
    {
        if (this.model.BackupTypes.Count > 0)
        {
            string message = string.IsNullOrEmpty(this.model.BackupSetPath)
                ? "The current backups are not saved.  If you want to save them before closing click cancel.  If you want to close the window and loose all your data click ok"
                : "If you have ensured that the backup set is saved, or you do not want to save it click ok.  If you are unsure wether the backupset is saved or not click cancel";

            var ok = await ShowConfirm(message,
                string.IsNullOrEmpty(this.model.BackupSetPath) ? "!!!Warning!!! Backups not saved." : "Are you sure you want to close?");

            // ok == true means "close" -> the close should proceed; cancel means proceed=false
            return !ok;
        }

        return false;
    }

    // ----- helpers ----------------------------------------------------------

    private void AppendLog(string line)
    {
        string current = this.model.Log;
        this.model.Log = string.IsNullOrEmpty(current) ? line : current + Environment.NewLine + line;
    }

    /**
     * <summary>
     *  nac.Forms has no message box.  Confirmation prompts are a small child
     *  form with OK / Cancel buttons.  The chosen result is captured in a
     *  PromptResult and read back after the dialog closes.
     * </summary>
     */
    private async Task<bool> ShowConfirm(string message, string title)
    {
        var result = new PromptResult();

        await mainForm.DisplayChildForm(child =>
        {
            child.Title = title;

            child.Text(message, style: "margin:10px;");

            child.HorizontalGroup(g =>
            {
                g.Button("OK", async () =>
                {
                    result.Confirmed = true;
                    child.Close();
                });
                g.Button("Cancel", async () =>
                {
                    result.Confirmed = false;
                    child.Close();
                });
            });
        }, isDialog: true);

        return result.Confirmed;
    }
}
