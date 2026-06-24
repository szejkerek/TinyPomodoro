using System.Globalization;

namespace Pomodoro.Services
{
    /// <summary>
    /// Formats a task's due date into a short, source-agnostic display string (e.g. "📅 Jun 26").
    /// Each source stores it differently — Todoist as an ISO date string, ClickUp as Unix
    /// milliseconds — so there is one parser per source and one shared format. Returns "" when unset.
    /// </summary>
    public static class DueDateLabel
    {
        public static string FromTodoist(string? isoDate)
        {
            if (string.IsNullOrEmpty(isoDate))
            {
                return string.Empty;
            }

            bool parsed = DateTime.TryParse(
                isoDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date);
            return parsed ? Format(date) : string.Empty;
        }

        public static string FromClickUpMillis(string? millis)
        {
            if (string.IsNullOrEmpty(millis) || long.TryParse(millis, out long value) == false || value <= 0)
            {
                return string.Empty;
            }

            return Format(DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime);
        }

        private static string Format(DateTime date)
        {
            return $"📅 {date.ToString("MMM d", CultureInfo.InvariantCulture)}";
        }
    }
}
