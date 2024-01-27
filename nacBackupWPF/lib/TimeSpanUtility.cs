using System;
using System.Text;

namespace nacBackupWPF.lib;

public static class TimeSpanUtility
{

    public static string GetTimeSpanText(TimeSpan timeDiff)
    {
        int AverageDaysInAMonth = 30;
        int AverageDaysInAYear = 365;
        int DaysInAWeek = 7;
        int daysLeft = Math.Abs(timeDiff.Days);

        int Years = (int)Math.Floor((double)daysLeft / AverageDaysInAYear);

        daysLeft -= (Years * AverageDaysInAYear); // we calculated the years so take it out of the days left

        int Months = (int)Math.Floor((double)daysLeft / AverageDaysInAMonth); // use 30 for the average days in a month

        daysLeft -= (Months * AverageDaysInAMonth); // take the months we calculated out

        int Weeks = (int)Math.Floor((double)daysLeft / DaysInAWeek);

        daysLeft -= (Weeks * DaysInAWeek);

        int Days = daysLeft; // and finally days get the left overs

        int Hours = timeDiff.Hours;
        int Minutes = timeDiff.Minutes;
        int Seconds = timeDiff.Seconds;

        StringBuilder sb = new StringBuilder();

        if( Years > 0 )
        {
            sb.Append($"{Years} Years ");
        }

        if( Months > 0 )
        {
            sb.Append($"{Months} Months ");
        }

        if (Weeks > 0)
        {
            sb.Append($"{Weeks} Weeks ");
        }

        if (Days > 0)
        {
            sb.Append($"{Days} Days ");
        }

        if (Hours > 0)
        {
            sb.Append($"{Hours} Hours ");
        }

        if (Minutes > 0)
        {
            sb.Append($"{Minutes} Minutes ");
        }

        if (Seconds > 0)
        {
            sb.Append($"{Seconds} Seconds");
        }

        return sb.ToString().Trim();
    }
    
    
}