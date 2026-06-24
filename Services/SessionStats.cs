using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>Pure read-models over the session log: streaks, heatmaps, daily counts. No disk, no clock.</summary>
    public static class SessionStats
    {
        private const int DaysPerWeek = 7;
        private const int HoursPerDay = 24;

        /// <summary>
        /// Counts of completed pomodoros per [day-of-week, hour] slot. The first index is
        /// <see cref="DayOfWeek"/> as an int (Sunday = 0); the UI reorders to a Monday-first week.
        /// </summary>
        public static int[,] WeeklyHeatmap(IReadOnlyList<CompletedPomodoro> entries)
        {
            int[,] grid = new int[DaysPerWeek, HoursPerDay];
            foreach (CompletedPomodoro entry in entries)
            {
                int day = (int)entry.CompletedAt.DayOfWeek;
                int hour = entry.CompletedAt.Hour;
                grid[day, hour]++;
            }

            return grid;
        }

        public static int CurrentStreak(IReadOnlyList<CompletedPomodoro> entries, DateTime today)
        {
            HashSet<DateTime> activeDays = new HashSet<DateTime>(entries.Select(entry => entry.CompletedAt.Date));

            // The streak survives a still-young today: anchor on today if it has a pomodoro,
            // otherwise on yesterday. Only two idle days in a row truly break it.
            DateTime anchor = today.Date;
            if (activeDays.Contains(anchor) == false)
            {
                anchor = anchor.AddDays(-1);
            }

            int streak = 0;
            DateTime day = anchor;
            while (activeDays.Contains(day))
            {
                streak++;
                day = day.AddDays(-1);
            }

            return streak;
        }
    }
}
