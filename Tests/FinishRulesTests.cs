using Pomodoro.Models;
using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class FinishRulesTests
    {
        [Theory]
        [InlineData(TimerMode.Pomodoro, true)]
        [InlineData(TimerMode.ShortBreak, false)]
        [InlineData(TimerMode.LongBreak, false)]
        public void Only_a_finished_pomodoro_is_recorded(TimerMode finishedMode, bool expected)
        {
            Assert.Equal(expected, PomodoroEngine.ShouldRecord(finishedMode));
        }

        [Theory]
        [InlineData(TimerMode.Pomodoro, true, false, true)]   // after pomodoro -> follow AutoStartBreaks
        [InlineData(TimerMode.Pomodoro, false, true, false)]
        [InlineData(TimerMode.ShortBreak, false, true, true)] // after break -> follow AutoStartPomodoros
        [InlineData(TimerMode.LongBreak, true, false, false)]
        public void Auto_start_after_a_finish_follows_the_matching_setting(
            TimerMode finishedMode, bool autoStartBreaks, bool autoStartPomodoros, bool expected)
        {
            AppSettings settings = new AppSettings
            {
                AutoStartBreaks = autoStartBreaks,
                AutoStartPomodoros = autoStartPomodoros
            };

            Assert.Equal(expected, PomodoroEngine.ShouldAutoStart(finishedMode, settings));
        }
    }
}
