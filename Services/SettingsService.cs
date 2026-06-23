using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// Owns the live <see cref="AppSettings"/> instance and is the single place that persists it.
    /// Every mutation goes through here, so "when does it save?" has one answer.
    /// </summary>
    public sealed class SettingsService
    {
        private readonly ISettingsStore store;

        public SettingsService(ISettingsStore store)
        {
            this.store = store;
            Current = store.Load();
        }

        public AppSettings Current { get; }

        public event Action? Changed;

        /// <summary>Mutate one or more fields, persist, and notify.</summary>
        public void Update(Action<AppSettings> mutate)
        {
            mutate(Current);
            Save();
        }

        /// <summary>Persist the current instance after it was edited in place (e.g. by the settings dialog).</summary>
        public void Save()
        {
            store.Save(Current);
            Changed?.Invoke();
        }
    }
}
