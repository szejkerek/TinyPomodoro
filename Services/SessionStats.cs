using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>Pure read-models over the session log: streaks, heatmaps, daily counts. No disk, no clock.</summary>
    public static class SessionStats
    {
        private const int DaysPerWeek = 7;
        private const int HoursPerDay = 24;
        private static readonly int SourceCount = Enum.GetValues(typeof(TaskSource)).Length;

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

        /// <summary>
        /// Like <see cref="WeeklyHeatmap"/> but split by source: the third index is the
        /// <see cref="TaskSource"/> as an int, so the UI can colour each slot by context.
        /// </summary>
        public static int[,,] WeeklySourceHeatmap(IReadOnlyList<CompletedPomodoro> entries)
        {
            int[,,] grid = new int[DaysPerWeek, HoursPerDay, SourceCount];
            foreach (CompletedPomodoro entry in entries)
            {
                int source = (int)entry.Source;
                if (source < 0 || source >= SourceCount)
                {
                    continue;
                }

                int day = (int)entry.CompletedAt.DayOfWeek;
                int hour = entry.CompletedAt.Hour;
                grid[day, hour, source]++;
            }

            return grid;
        }

        /// <summary>
        /// The busiest slot in a source-split heatmap: the largest sum across sources for any
        /// [day, hour] cell. Used to normalise heat intensity. Returns 0 for an all-zero grid.
        /// </summary>
        public static int Peak(int[,,] grid)
        {
            int days = grid.GetLength(0);
            int hours = grid.GetLength(1);
            int sources = grid.GetLength(2);

            int peak = 0;
            for (int day = 0; day < days; day++)
            {
                for (int hour = 0; hour < hours; hour++)
                {
                    int total = 0;
                    for (int source = 0; source < sources; source++)
                    {
                        total += grid[day, hour, source];
                    }

                    if (total > peak)
                    {
                        peak = total;
                    }
                }
            }

            return peak;
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
