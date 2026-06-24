namespace Pomodoro.Models
{
    public sealed class AppSettings
    {
        public int PomodoroMinutes { get; set; } = 25;
        public int ShortBreakMinutes { get; set; } = 5;
        public int LongBreakMinutes { get; set; } = 15;
        public int LongBreakInterval { get; set; } = 4;

        public bool AutoStartBreaks { get; set; } = false;
        public bool AutoStartPomodoros { get; set; } = false;
        public bool SoundEnabled { get; set; } = true;
        public bool StartWithWindows { get; set; } = true;

        public TaskSource ActiveSource { get; set; } = TaskSource.Todoist;

        public string TodoistToken { get; set; } = string.Empty;
        public string TodoistFilter { get; set; } = string.Empty;
        public string SelectedProjectId { get; set; } = string.Empty;

        public string ClickUpToken { get; set; } = string.Empty;
        public string ClickUpListId { get; set; } = string.Empty;

        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }

        public int MinutesFor(TimerMode mode)
        {
            if (mode == TimerMode.ShortBreak)
            {
                return ShortBreakMinutes;
            }

            if (mode == TimerMode.LongBreak)
            {
                return LongBreakMinutes;
            }

            return PomodoroMinutes;
        }
    }
}
