using System.Windows.Media;

namespace Pomodoro.Presentation
{
    /// <summary>
    /// Colours for a task's workflow status tag (ClickUp). A list's status names are custom, so the
    /// column is recognised by the same name fragments the gateway uses ("progress", "review",
    /// "done"/"closed"), and anything else reads as the not-started colour.
    /// </summary>
    public static class StatusTheme
    {
        public static readonly Color ToDo = Color.FromRgb(0x8A, 0xA0, 0xB8);       // slate blue-grey
        public static readonly Color InProgress = Color.FromRgb(0xE0, 0xA4, 0x4E); // amber
        public static readonly Color Review = Color.FromRgb(0xB0, 0x84, 0xE8);     // violet
        public static readonly Color Done = Color.FromRgb(0x6F, 0xC2, 0x8B);       // green

        public static Color For(string status)
        {
            string normalized = status.ToLowerInvariant();

            if (normalized.Contains("progress"))
            {
                return InProgress;
            }

            if (normalized.Contains("review"))
            {
                return Review;
            }

            if (normalized.Contains("done") || normalized.Contains("complete") || normalized.Contains("closed"))
            {
                return Done;
            }

            return ToDo;
        }
    }
}
