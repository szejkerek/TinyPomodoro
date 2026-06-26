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

        public bool FocusRadioEnabled { get; set; } = false;
        public int RadioStationIndex { get; set; } = 0;
        public double RadioVolume { get; set; } = 0.5;

        public TaskSource ActiveSource { get; set; } = TaskSource.Todoist;

        public string TodoistToken { get; set; } = string.Empty;
        public string TodoistFilter { get; set; } = string.Empty;
        public string SelectedProjectId { get; set; } = string.Empty;

        public string ClickUpToken { get; set; } = string.Empty;
        public string ClickUpListId { get; set; } = string.Empty;

        public bool BlockDistractionsEnabled { get; set; } = true;
        public string BlockedHosts { get; set; } = DefaultBlockedHosts;
        public string BlockedProcesses { get; set; } = DefaultBlockedProcesses;

        public bool BlockWorkToolsOutsideWork { get; set; } = true;
        public string WorkBlockedHosts { get; set; } = DefaultWorkBlockedHosts;
        public string WorkBlockedProcesses { get; set; } = DefaultWorkBlockedProcesses;

        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }

        private const string DefaultBlockedHosts =
            "youtube.com\nfacebook.com\nlinkedin.com\nmail.google.com\noutlook.live.com\noutlook.office.com";

        private const string DefaultBlockedProcesses =
            "Spotify\nsteam\nDiscord";

        private const string DefaultWorkBlockedHosts =
            "asana.com\napp.asana.com\nslack.com";

        private const string DefaultWorkBlockedProcesses =
            "Slack";

        /// <summary>The base blocked domains, one per line, trimmed and without blanks.</summary>
        public IReadOnlyList<string> BlockedHostList()
        {
            return SplitLines(BlockedHosts, stripExeSuffix: false);
        }

        /// <summary>The base blocked process names, one per line, trimmed, with any trailing ".exe" removed.</summary>
        public IReadOnlyList<string> BlockedProcessList()
        {
            return SplitLines(BlockedProcesses, stripExeSuffix: true);
        }

        /// <summary>Work-tool domains (Asana, Slack, …) — blocked only when the active context isn't work.</summary>
        public IReadOnlyList<string> WorkBlockedHostList()
        {
            return SplitLines(WorkBlockedHosts, stripExeSuffix: false);
        }

        /// <summary>Work-tool process names — blocked only when the active context isn't work.</summary>
        public IReadOnlyList<string> WorkBlockedProcessList()
        {
            return SplitLines(WorkBlockedProcesses, stripExeSuffix: true);
        }

        /// <summary>
        /// The domains to block for the current focus session: the always-blocked list, plus the
        /// work-tool domains unless the active context is work (where those tools are needed).
        /// </summary>
        public IReadOnlyList<string> ActiveBlockedHostList()
        {
            return CombineForActiveContext(BlockedHostList(), WorkBlockedHostList());
        }

        /// <summary>The processes to block for the current focus session; see <see cref="ActiveBlockedHostList"/>.</summary>
        public IReadOnlyList<string> ActiveBlockedProcessList()
        {
            return CombineForActiveContext(BlockedProcessList(), WorkBlockedProcessList());
        }

        private bool IsWorkContext => ActiveSource == TaskSource.Asana;

        private IReadOnlyList<string> CombineForActiveContext(
            IReadOnlyList<string> always,
            IReadOnlyList<string> workTools)
        {
            List<string> combined = new List<string>(always);
            if (BlockWorkToolsOutsideWork && IsWorkContext == false)
            {
                combined.AddRange(workTools);
            }

            return combined;
        }

        private static IReadOnlyList<string> SplitLines(string text, bool stripExeSuffix)
        {
            List<string> entries = new List<string>();
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (stripExeSuffix && line.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    line = line.Substring(0, line.Length - ".exe".Length);
                }

                entries.Add(line);
            }

            return entries;
        }

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
