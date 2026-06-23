using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>Pure timer state machine. No UI, no threads — the view ticks it once per second.</summary>
    public sealed class PomodoroEngine
    {
        private const int SecondsPerMinute = 60;

        private readonly AppSettings settings;

        public PomodoroEngine(AppSettings settings)
        {
            this.settings = settings;
            CurrentMode = TimerMode.Pomodoro;
            RemainingSeconds = settings.MinutesFor(TimerMode.Pomodoro) * SecondsPerMinute;
        }

        public TimerMode CurrentMode { get; private set; }
        public int RemainingSeconds { get; private set; }
        public bool IsRunning { get; private set; }
        public int CompletedPomodoros { get; private set; }

        public void SwitchTo(TimerMode mode)
        {
            CurrentMode = mode;
            RemainingSeconds = settings.MinutesFor(mode) * SecondsPerMinute;
            IsRunning = false;
        }

        public void ResetCurrentMode()
        {
            RemainingSeconds = settings.MinutesFor(CurrentMode) * SecondsPerMinute;
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
            if (!IsRunning)
            {
                return false;
            }

            RemainingSeconds--;
            if (RemainingSeconds > 0)
            {
                return false;
            }

            RemainingSeconds = 0;
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
    }
}
