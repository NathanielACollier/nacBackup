using System;
using System.Globalization;

namespace nacBackupWPF.lib;

public static class ByteSize
{
    public static string BytesToString(this long byteCount)
    {
        string result = "";

        // The if statements need to start high and work there way down.  If you don't do that then everything will be caught by the first one
        //  This uses a sieve algorithm.  The bigger numbers are caught by the sieve, and the smaller ones continue to fall through untill the hole gets smaller and smaller.
        //  Untill finally they are caught at the bottom of the bag as bytes.
        if (byteCount >= Math.Pow(1024, 3))
        {
            // display as gb
            result = (byteCount / Math.Pow(1024, 3)).ToString("#,#.##", CultureInfo.InvariantCulture) + " GB";
        }
        else if (byteCount >= Math.Pow(1024, 2))
        {
            // display as mb
            result = (byteCount / Math.Pow(1024, 2)).ToString("#,#.##", CultureInfo.InvariantCulture) + " MB";
        }
        else if (byteCount >= 1024)
        {
            // display as kb
            result = (byteCount / 1024).ToString("#,#.##", CultureInfo.InvariantCulture) + " KB";
        }
        else
        {
            // display as bytes
            result = byteCount.ToString("#,#.##", CultureInfo.InvariantCulture) + " Bytes";
        }

        return result;
    }

}