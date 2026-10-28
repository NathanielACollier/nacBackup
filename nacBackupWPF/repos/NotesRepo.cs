using System;
using System.IO;
using nacBackupWPF.models;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  Reads and writes the free-form notes the user enters.  The notes live in
 *  model.Notes and are persisted to the notes file on disk.  Both the WPF and
 *  Avalonia UIs call the same methods here (the UI just wires its text box to
 *  model.Notes).
 * </summary>
 */
public class NotesRepo
{
    private readonly MainViewModel model;
    private readonly AppPaths app;
    private readonly UiContext ui;

    public NotesRepo(MainViewModel model, AppPaths app, UiContext ui)
    {
        this.model = model;
        this.app = app;
        this.ui = ui;
    }

    /**
     * <summary>
     *  Load any saved notes into the model on startup.  If the file exists but
     *  is empty it is deleted.
     * </summary>
     */
    public void PopulateNotesFromNotesFile()
    {
        if (File.Exists(app.NotesFilePath))
        {
            string notes = File.ReadAllText(app.NotesFilePath);

            if (string.IsNullOrEmpty(notes))
            {
                File.Delete(app.NotesFilePath);
            }
            else
            {
                model.Notes = notes;
            }
        }
    }

    /**
     * <summary>
     *  Persist the current notes text to disk.  Called from the save-notes
     *  button and when the window is closing (so the user does not lose text).
     * </summary>
     */
    public void SaveNotes()
    {
        File.WriteAllText(app.NotesFilePath, model.Notes);
    }

    /**
     * <summary>
     *  Clear the notes in the model and delete the notes file so the next
     *  Populates starts clean.
     * </summary>
     */
    public void ClearNotes()
    {
        model.Notes = string.Empty;

        if (File.Exists(app.NotesFilePath))
        {
            File.Delete(app.NotesFilePath);
        }
    }
}
