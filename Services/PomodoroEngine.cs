using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>Pure timer state machine. No UI, no threads — the view ticks it once per second.</summary>
    public sealed class PomodoroEngine
    {
        private const int SecondsPerMinute = 60;

        private readonly AppSettings settings;

        // Each mode keeps its own countdown, so switching tabs while paused never loses progress.
        private readonly Dictionary<TimerMode, int> remainingByMode = new Dictionary<TimerMode, int>();

        public PomodoroEngine(AppSettings settings)
        {
            this.settings = settings;
            foreach (TimerMode mode in Enum.GetValues<TimerMode>())
            {
                remainingByMode[mode] = FullSeconds(mode);
            }

            CurrentMode = TimerMode.Pomodoro;
        }

        public TimerMode CurrentMode { get; private set; }
        public int RemainingSeconds => remainingByMode[CurrentMode];
        public bool IsRunning { get; private set; }
        public int CompletedPomodoros { get; private set; }

        /// <summary>Manual mode switch (tabs): keeps each mode's parked time intact.</summary>
        public void SwitchTo(TimerMode mode)
        {
            CurrentMode = mode;
            IsRunning = false;
        }

        /// <summary>Auto-advance after a finish: both the leaving and entering modes start fresh.</summary>
        public void AdvanceTo(TimerMode mode)
        {
            remainingByMode[CurrentMode] = FullSeconds(CurrentMode);
            remainingByMode[mode] = FullSeconds(mode);
            CurrentMode = mode;
            IsRunning = false;
        }

        public void ResetCurrentMode()
        {
            remainingByMode[CurrentMode] = FullSeconds(CurrentMode);
            IsRunning = false;
        }

        public void Start()
        {
            IsRunning = true;
        }

        public void Pause()
        {
            IsRunning = false;
        }

        /// <summary>Decrements one second. Returns true when the current mode just finished.</summary>
        public bool TickOneSecond()
        {
            if (IsRunning == false)
            {
                return false;
            }

            remainingByMode[CurrentMode]--;
            if (RemainingSeconds > 0)
            {
                return false;
            }

            remainingByMode[CurrentMode] = 0;
            IsRunning = false;
            return true;
        }

        /// <summary>Moves to the mode that naturally follows the one that just finished.</summary>
        public TimerMode SelectNextMode()
        {
            if (CurrentMode != TimerMode.Pomodoro)
            {
                return TimerMode.Pomodoro;
            }

            CompletedPomodoros++;
            bool isLongBreakDue = CompletedPomodoros % settings.LongBreakInterval == 0;
            return isLongBreakDue ? TimerMode.LongBreak : TimerMode.ShortBreak;
        }

        private int FullSeconds(TimerMode mode)
        {
            return settings.MinutesFor(mode) * SecondsPerMinute;
        }
    }
}
