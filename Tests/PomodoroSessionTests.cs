using Pomodoro.Models;
using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class PomodoroSessionTests
    {
        private static AppSettings OneMinuteSettings()
        {
            return new AppSettings
            {
                PomodoroMinutes = 1,
                ShortBreakMinutes = 1,
                LongBreakMinutes = 1,
                LongBreakInterval = 4,
                AutoStartBreaks = false,
                AutoStartPomodoros = false
            };
        }

        [Fact]
        public void Starts_in_pomodoro_at_full_duration()
        {
            PomodoroSession session = new PomodoroSession(OneMinuteSettings(), new ManualClock());

            Assert.Equal(TimerMode.Pomodoro, session.CurrentMode);
            Assert.Equal(60, session.RemainingSeconds);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Finishing_a_pomodoro_moves_to_short_break_and_counts_it()
        {
            ManualClock clock = new ManualClock();
            PomodoroSession session = new PomodoroSession(OneMinuteSettings(), clock);
            int finishedCount = 0;
            session.Finished += () => finishedCount++;

            session.Start();
            clock.Advance(60);

            Assert.Equal(1, finishedCount);
            Assert.Equal(TimerMode.ShortBreak, session.CurrentMode);
            Assert.Equal(1, session.CompletedPomodoros);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Every_fourth_pomodoro_yields_a_long_break()
        {
            AppSettings settings = OneMinuteSettings();
            settings.AutoStartBreaks = true;
            settings.AutoStartPomodoros = true;

            ManualClock clock = new ManualClock();
            PomodoroSession session = new PomodoroSession(settings, clock);

            session.Start();
            // 4 pomodoros + 3 short breaks chained by auto-start = 7 finished minutes,
            // landing on the long break after the 4th pomodoro.
            clock.Advance(60); // pomodoro 1 -> short break (auto)
            clock.Advance(60); // short break -> pomodoro 2
            clock.Advance(60); // pomodoro 2 -> short break
            clock.Advance(60); // short break -> pomodoro 3
            clock.Advance(60); // pomodoro 3 -> short break
            clock.Advance(60); // short break -> pomodoro 4
            clock.Advance(60); // pomodoro 4 -> LONG break

            Assert.Equal(TimerMode.LongBreak, session.CurrentMode);
            Assert.Equal(4, session.CompletedPomodoros);
        }

        [Fact]
        public void Auto_start_breaks_keeps_the_clock_running_after_a_pomodoro()
        {
            AppSettings settings = OneMinuteSettings();
            settings.AutoStartBreaks = true;

            ManualClock clock = new ManualClock();
            PomodoroSession session = new PomodoroSession(settings, clock);

            session.Start();
            clock.Advance(60);

            Assert.Equal(TimerMode.ShortBreak, session.CurrentMode);
            Assert.True(session.IsRunning);
        }

        [Fact]
        public void Reset_restores_full_duration_and_stops()
        {
            ManualClock clock = new ManualClock();
            PomodoroSession session = new PomodoroSession(OneMinuteSettings(), clock);

            session.Start();
            clock.Advance(20);
            session.Reset();

            Assert.Equal(60, session.RemainingSeconds);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Changed_fires_on_each_tick()
        {
            ManualClock clock = new ManualClock();
            PomodoroSession session = new PomodoroSession(OneMinuteSettings(), clock);
            int changedCount = 0;
            session.Changed += () => changedCount++;

            session.Start();   // 1
            clock.Advance(3);  // 3 more

            Assert.True(changedCount >= 4);
            Assert.Equal(57, session.RemainingSeconds);
        }
    }
}
