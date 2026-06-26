using System;
using System.Windows.Media;

namespace Pomodoro.Services
{
    /// <summary>
    /// <see cref="IRadioPlayer"/> over WPF's built-in <see cref="MediaPlayer"/>, which plays HTTP
    /// icecast MP3 streams directly — no extra dependency. The streams are continuous, so there is
    /// nothing to loop; a failed open simply leaves the player silent.
    /// </summary>
    public sealed class MediaPlayerRadio : IRadioPlayer
    {
        private readonly MediaPlayer player = new MediaPlayer();

        public void Load(Uri stream)
        {
            player.Open(stream);
        }

        public void Play()
        {
            player.Play();
        }

        public void Pause()
        {
            player.Pause();
        }

        public double Volume
        {
            set => player.Volume = value;
        }
    }
}
