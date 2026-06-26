using System;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// Owns the focus-radio flow: which station is selected, the volume, and whether it's playing.
    /// Drives an <see cref="IRadioPlayer"/> and persists the station/volume through
    /// <see cref="SettingsService"/> (the one place settings are saved). The window binds to its
    /// state and re-renders on <see cref="Changed"/>. Playback follows the focus session: it starts
    /// when focus begins (opt-in via <see cref="AppSettings.FocusRadioEnabled"/>) and stops when it
    /// ends — the only manual control during focus is mute.
    /// </summary>
    public sealed class RadioModel
    {
        private const double MinVolume = 0.0;
        private const double MaxVolume = 1.0;

        private readonly IRadioPlayer player;
        private readonly SettingsService settings;

        private int stationIndex;
        private bool isStreamLoaded;
        private bool isFocusActive;

        public RadioModel(IRadioPlayer player, SettingsService settings)
        {
            this.player = player;
            this.settings = settings;

            stationIndex = ClampStationIndex(settings.Current.RadioStationIndex);
            Volume = Clamp(settings.Current.RadioVolume, MinVolume, MaxVolume);
            player.Volume = Volume;
        }

        /// <summary>Fired when station, volume, mute, or playback state changes, so the window re-renders.</summary>
        public event Action? Changed;

        public RadioStation CurrentStation => RadioStations.All[stationIndex];

        public bool IsPlaying { get; private set; }

        public bool IsMuted { get; private set; }

        public double Volume { get; private set; }

        /// <summary>True when the panel should show: focus is active and the radio is enabled in settings.</summary>
        public bool IsActive => isFocusActive && settings.Current.FocusRadioEnabled;

        /// <summary>Focus started or stopped — start the radio on entry (unless muted), stop it on exit.</summary>
        public void FollowFocus(bool focusActive)
        {
            if (isFocusActive == focusActive)
            {
                return;
            }

            isFocusActive = focusActive;
            ApplyPlayback();
            Changed?.Invoke();
        }

        /// <summary>Silence the radio without leaving focus; toggling back resumes it.</summary>
        public void ToggleMute()
        {
            IsMuted = !IsMuted;
            ApplyPlayback();
            Changed?.Invoke();
        }

        /// <summary>Advance to the next station (wrapping), reload it, and resume only if already playing.</summary>
        public void Skip()
        {
            stationIndex = (stationIndex + 1) % RadioStations.All.Count;
            settings.Update(current => current.RadioStationIndex = stationIndex);

            player.Load(CurrentStation.StreamUri);
            isStreamLoaded = true;

            if (IsPlaying)
            {
                player.Play();
            }

            Changed?.Invoke();
        }

        public void SetVolume(double value)
        {
            double clamped = Clamp(value, MinVolume, MaxVolume);
            Volume = clamped;
            player.Volume = clamped;
            settings.Update(current => current.RadioVolume = clamped);
            Changed?.Invoke();
        }

        private void ApplyPlayback()
        {
            bool shouldPlay = isFocusActive && settings.Current.FocusRadioEnabled && IsMuted == false;
            if (shouldPlay == IsPlaying)
            {
                return;
            }

            if (shouldPlay)
            {
                EnsureStreamLoaded();
                player.Play();
                IsPlaying = true;
                return;
            }

            player.Pause();
            IsPlaying = false;
        }

        private void EnsureStreamLoaded()
        {
            if (isStreamLoaded)
            {
                return;
            }

            player.Load(CurrentStation.StreamUri);
            isStreamLoaded = true;
        }

        private static int ClampStationIndex(int index)
        {
            if (index < 0 || index >= RadioStations.All.Count)
            {
                return 0;
            }

            return index;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
