namespace nacBackupWPF.lib;

public class settings
{
    public static string appVersion
    {
        get
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();

            return proc.MainModule?.FileVersionInfo?.FileVersion;
        }
    }
    
    
    
}