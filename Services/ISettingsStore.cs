using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>Persistence seam for settings. JSON on disk in prod, in-memory in tests.</summary>
    public interface ISettingsStore
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }
}
