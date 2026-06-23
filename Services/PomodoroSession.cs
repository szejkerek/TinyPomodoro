using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// The deep timer module. Owns the <see cref="PomodoroEngine"/> and an <see cref="IClock"/>,
    /// and runs the whole session lifecycle — ticking, the finish sequence, and the auto-start chain —
    /// behind three events. Callers (and tests) only touch this interface.
    /// </summary>
    public sealed class PomodoroSession
    {
        private const int SecondsPerMinute = 60;

        private readonly IClock clock;
        private readonly AppSettings settings;
        private readonly ISessionLog? sessionLog;
        private PomodoroEngine engine;

        public PomodoroSession(AppSettings settings, IClock clock, ISessionLog? sessionLog = null)
        {
            this.settings = settings;
            this.clock = clock;
            this.sessionLog = sessionLog;
            engine = new PomodoroEngine(settings);
            clock.Tick += OnClockTick;
        }

        public TimerMode CurrentMode => engine.CurrentMode;
        public int RemainingSeconds => engine.RemainingSeconds;
        public bool IsRunning => engine.IsRunning;
        public int CompletedPomodoros => engine.CompletedPomodoros;

        /// <summary>Anything that changes what the UI should show (every second + on state change).</summary>
        public event Action? Changed;

        /// <summary>A mode reached zero. Edge for platform side effects (e.g. the alarm sound).</summary>
        public event Action? Finished;

        public void ToggleStartPause()
        {
            if (engine.IsRunning)
            {
                engine.Pause();
                clock.Stop();
                Changed?.Invoke();
                return;
            }

            Start();
        }

        public void Start()
        {
            engine.Start();
            clock.Start();
            Changed?.Invoke();
        }

        public void Reset()
        {
            clock.Stop();
            engine.ResetCurrentMode();
            Changed?.Invoke();
        }

        public void SwitchTo(TimerMode mode)
        {
            if (engine.IsRunning)
            {
                return;
            }

            clock.Stop();
            engine.SwitchTo(mode);
            Changed?.Invoke();
        }

        /// <summary>Re-read settings (durations, interval) and start a fresh session.</summary>
        public void ApplySettings()
        {
            clock.Stop();
            engine = new PomodoroEngine(settings);
            Changed?.Invoke();
        }

        private void OnClockTick()
        {
            bool didFinish = engine.TickOneSecond();
            Changed?.Invoke();

            if (!didFinish)
            {
                return;
            }

            clock.Stop();
            RunFinishSequence();
        }

        private void RunFinishSequence()
        {
            bool wasPomodoro = engine.CurrentMode == TimerMode.Pomodoro;
            if (wasPomodoro)
            {
                RecordCompletedPomodoro();
            }

            TimerMode nextMode = engine.SelectNextMode();
            engine.AdvanceTo(nextMode);

            Finished?.Invoke();
            Changed?.Invoke();

            bool shouldAutoStart = wasPomodoro ? settings.AutoStartBreaks : settings.AutoStartPomodoros;
            if (shouldAutoStart)
            {
                Start();
            }
        }

        private void RecordCompletedPomodoro()
        {
            if (sessionLog == null)
            {
                return;
            }

            int durationSeconds = settings.PomodoroMinutes * SecondsPerMinute;
            sessionLog.Record(new CompletedPomodoro(clock.Now, durationSeconds, null));
        }
    }
}
