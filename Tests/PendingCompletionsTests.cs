using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class PendingCompletionsTests
    {
        private const int DelaySeconds = 2;

        [Fact]
        public void Completion_elapses_once_after_the_delay()
        {
            ManualClock clock = new ManualClock();
            PendingCompletions pending = new PendingCompletions(clock, DelaySeconds);
            List<string> elapsed = new List<string>();
            pending.Elapsed += id => elapsed.Add(id);

            pending.Begin("task-1");
            clock.Advance(DelaySeconds);

            Assert.Equal(new[] { "task-1" }, elapsed);
        }

        [Fact]
        public void Cancelling_before_the_delay_prevents_the_completion()
        {
            ManualClock clock = new ManualClock();
            PendingCompletions pending = new PendingCompletions(clock, DelaySeconds);
            List<string> elapsed = new List<string>();
            pending.Elapsed += id => elapsed.Add(id);

            pending.Begin("task-1");
            clock.Advance(DelaySeconds - 1);
            pending.Cancel("task-1");
            clock.Advance(DelaySeconds);

            Assert.Empty(elapsed);
            Assert.False(pending.IsPending("task-1"));
        }

        [Fact]
        public void A_completion_fires_only_once_even_if_more_ticks_pass()
        {
            ManualClock clock = new ManualClock();
            PendingCompletions pending = new PendingCompletions(clock, DelaySeconds);
            List<string> elapsed = new List<string>();
            pending.Elapsed += id => elapsed.Add(id);

            pending.Begin("task-1");
            clock.Advance(DelaySeconds * 3);

            Assert.Equal(new[] { "task-1" }, elapsed);
        }

        [Fact]
        public void The_clock_stops_once_nothing_is_pending()
        {
            ManualClock clock = new ManualClock();
            PendingCompletions pending = new PendingCompletions(clock, DelaySeconds);

            pending.Begin("task-1");
            clock.Advance(DelaySeconds);

            Assert.False(clock.IsRunning);
        }

        [Fact]
        public void ClearAll_cancels_every_pending_completion()
        {
            ManualClock clock = new ManualClock();
            PendingCompletions pending = new PendingCompletions(clock, DelaySeconds);
            List<string> elapsed = new List<string>();
            pending.Elapsed += id => elapsed.Add(id);

            pending.Begin("task-1");
            pending.Begin("task-2");
            pending.ClearAll();
            clock.Advance(DelaySeconds);

            Assert.Empty(elapsed);
            Assert.False(pending.IsPending("task-1"));
            Assert.False(pending.IsPending("task-2"));
        }

        [Fact]
        public void Cancelling_one_task_leaves_the_other_pending()
        {
            ManualClock clock = new ManualClock();
            PendingCompletions pending = new PendingCompletions(clock, DelaySeconds);
            List<string> elapsed = new List<string>();
            pending.Elapsed += id => elapsed.Add(id);

            pending.Begin("task-1");
            pending.Begin("task-2");
            pending.Cancel("task-1");
            clock.Advance(DelaySeconds);

            Assert.Equal(new[] { "task-2" }, elapsed);
        }
    }
}
