using System;
using System.Diagnostics;
using System.IO;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  Framework-agnostic helpers for the file paths the application reads and
 *  writes (the notes file and the log file).
 * </summary>
 */
public class AppPaths
{
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
}
