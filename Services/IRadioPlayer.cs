using System;

namespace Pomodoro.Services
{
    /// <summary>
    /// The audio-playback seam for the focus radio. The platform adapter
    /// (<see cref="MediaPlayerRadio"/>) plays a stream; tests swap in a fake.
    /// </summary>
    public interface IRadioPlayer
    {
        /// <summary>Point the player at a stream, replacing whatever was loaded before.</summary>
        void Load(Uri stream);

        void Play();

        void Pause();

        /// <summary>Playback volume in the range [0, 1].</summary>
        double Volume { set; }
    }
}
