using System.Windows.Threading;

namespace Pomodoro.Services
{
    /// <summary>Production <see cref="IClock"/> adapter: a one-second WPF DispatcherTimer.</summary>
    public sealed class DispatcherClock : IClock
    {
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        public DispatcherClock()
        {
            timer.Tick += (_, _) => Tick?.Invoke();
        }

        public event Action? Tick;

        public void Start()
        {
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
        }
    }
}
