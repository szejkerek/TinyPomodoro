using System;
using System.Collections.Generic;

namespace Pomodoro.Models
{
    /// <summary>One internet-radio preset: a display name and the stream it points at.</summary>
    public sealed class RadioStation
    {
        public RadioStation(string name, string streamUrl)
        {
            Name = name;
            StreamUri = new Uri(streamUrl);
        }

        public string Name { get; }

        public Uri StreamUri { get; }
    }

    /// <summary>
    /// The fixed catalog of focus-radio presets. SomaFM icecast MP3 streams: listener-supported,
    /// allow direct streaming, and skew ambient/drone — a good fit for "focus noise".
    /// </summary>
    public static class RadioStations
    {
        public static IReadOnlyList<RadioStation> All { get; } = new List<RadioStation>
        {
            new RadioStation("Drone Zone", "https://ice1.somafm.com/dronezone-128-mp3"),
            new RadioStation("Deep Space One", "https://ice1.somafm.com/deepspaceone-128-mp3"),
            new RadioStation("Space Station Soma", "https://ice1.somafm.com/spacestation-128-mp3"),
            new RadioStation("Groove Salad", "https://ice1.somafm.com/groovesalad-128-mp3"),
            new RadioStation("Fluid", "https://ice1.somafm.com/fluid-128-mp3"),
        };
    }
}
