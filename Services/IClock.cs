namespace Pomodoro.Services
{
    /// <summary>
    /// The seam under the timer. Production drives it from a DispatcherTimer;
    /// tests drive it by calling <see cref="Advance"/> directly — no real time passes.
    /// </summary>
    public interface IClock
    {
        event Action? Tick;
        void Start();
        void Stop();
    }
}
