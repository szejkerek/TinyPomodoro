namespace Pomodoro.Services
{
    /// <summary>
    /// The undo window for task completion. After <see cref="Begin"/>, a task is held for a fixed
    /// number of seconds; if it is not <see cref="Cancel"/>led first, <see cref="Elapsed"/> fires
    /// once. Drives its own <see cref="IClock"/> (independent of the timer), so the countdown keeps
    /// running while a pomodoro is paused. UI-free: callers render the pending state and act on Elapsed.
    /// </summary>
    public sealed class PendingCompletions
    {
        private readonly IClock clock;
        private readonly int delaySeconds;
        private readonly Dictionary<string, int> remainingByTask = new Dictionary<string, int>();

        public PendingCompletions(IClock clock, int delaySeconds)
        {
            this.clock = clock;
            this.delaySeconds = delaySeconds;
            clock.Tick += OnTick;
        }

        /// <summary>A task's undo window ran out. The caller should now close the task.</summary>
        public event Action<string>? Elapsed;

        public bool IsPending(string taskId)
        {
            return remainingByTask.ContainsKey(taskId);
        }

        public void Begin(string taskId)
        {
            remainingByTask[taskId] = delaySeconds;
            clock.Start();
        }

        public void Cancel(string taskId)
        {
            remainingByTask.Remove(taskId);
            StopClockWhenIdle();
        }

        public void ClearAll()
        {
            remainingByTask.Clear();
            clock.Stop();
        }

        private void StopClockWhenIdle()
        {
            if (remainingByTask.Count == 0)
            {
                clock.Stop();
            }
        }

        private void OnTick()
        {
            List<string> finished = new List<string>();
            foreach (string taskId in remainingByTask.Keys.ToList())
            {
                remainingByTask[taskId]--;
                if (remainingByTask[taskId] <= 0)
                {
                    finished.Add(taskId);
                }
            }

            foreach (string taskId in finished)
            {
                remainingByTask.Remove(taskId);
            }

            StopClockWhenIdle();

            foreach (string taskId in finished)
            {
                Elapsed?.Invoke(taskId);
            }
        }
    }
}
