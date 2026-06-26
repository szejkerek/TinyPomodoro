using System;
using System.Collections.Generic;
using Pomodoro.Models;
using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class RadioModelTests
    {
        /// <summary>Test <see cref="IRadioPlayer"/>: records what it was told to do, plays nothing real.</summary>
        private sealed class RecordingRadioPlayer : IRadioPlayer
        {
            public List<Uri> Loaded { get; } = new List<Uri>();
            public int PlayCount { get; private set; }
            public int PauseCount { get; private set; }
            public double LastVolume { get; private set; } = -1;

            public void Load(Uri stream)
            {
                Loaded.Add(stream);
            }

            public void Play()
            {
                PlayCount++;
            }

            public void Pause()
            {
                PauseCount++;
            }

            public double Volume
            {
                set => LastVolume = value;
            }
        }

        private static (RadioModel model, RecordingRadioPlayer player, SettingsService settings) Build(AppSettings seed)
        {
            RecordingRadioPlayer player = new RecordingRadioPlayer();
            SettingsService settings = new SettingsService(new InMemorySettingsStore(seed));
            RadioModel model = new RadioModel(player, settings);
            return (model, player, settings);
        }

        private static AppSettings Enabled()
        {
            return new AppSettings { FocusRadioEnabled = true };
        }

        [Fact]
        public void Entering_focus_loads_current_station_then_plays_when_enabled()
        {
            (RadioModel model, RecordingRadioPlayer player, _) = Build(Enabled());

            model.FollowFocus(focusActive: true);

            Assert.True(model.IsPlaying);
            Assert.True(model.IsActive);
            Assert.Single(player.Loaded);
            Assert.Equal(RadioStations.All[0].StreamUri, player.Loaded[0]);
            Assert.Equal(1, player.PlayCount);
        }

        [Fact]
        public void Entering_focus_does_nothing_when_radio_disabled()
        {
            (RadioModel model, RecordingRadioPlayer player, _) = Build(new AppSettings());

            model.FollowFocus(focusActive: true);

            Assert.False(model.IsPlaying);
            Assert.False(model.IsActive);
            Assert.Empty(player.Loaded);
            Assert.Equal(0, player.PlayCount);
        }

        [Fact]
        public void Leaving_focus_pauses()
        {
            (RadioModel model, RecordingRadioPlayer player, _) = Build(Enabled());

            model.FollowFocus(focusActive: true);
            model.FollowFocus(focusActive: false);

            Assert.False(model.IsPlaying);
            Assert.False(model.IsActive);
            Assert.Equal(1, player.PauseCount);
        }

        [Fact]
        public void Re_entering_focus_reuses_the_loaded_stream_and_does_not_reload()
        {
            (RadioModel model, RecordingRadioPlayer player, _) = Build(Enabled());

            model.FollowFocus(focusActive: true);
            model.FollowFocus(focusActive: false);
            model.FollowFocus(focusActive: true);

            Assert.Single(player.Loaded);
            Assert.Equal(2, player.PlayCount);
        }

        [Fact]
        public void Mute_during_focus_pauses_then_unmute_resumes()
        {
            (RadioModel model, RecordingRadioPlayer player, _) = Build(Enabled());

            model.FollowFocus(focusActive: true);
            model.ToggleMute();

            Assert.True(model.IsMuted);
            Assert.False(model.IsPlaying);
            Assert.Equal(1, player.PauseCount);

            model.ToggleMute();

            Assert.False(model.IsMuted);
            Assert.True(model.IsPlaying);
            Assert.Equal(2, player.PlayCount);
        }

        [Fact]
        public void Skip_advances_with_wraparound_and_persists_index()
        {
            AppSettings seed = new AppSettings { RadioStationIndex = RadioStations.All.Count - 1 };
            (RadioModel model, _, SettingsService settings) = Build(seed);

            model.Skip();

            Assert.Equal(RadioStations.All[0].Name, model.CurrentStation.Name);
            Assert.Equal(0, settings.Current.RadioStationIndex);
        }

        [Fact]
        public void Skip_reloads_and_resumes_only_when_already_playing()
        {
            (RadioModel model, RecordingRadioPlayer player, _) = Build(Enabled());

            model.FollowFocus(focusActive: true);
            int playsBeforeSkip = player.PlayCount;
            model.Skip();

            Assert.Equal(RadioStations.All[1].StreamUri, player.Loaded[^1]);
            Assert.Equal(playsBeforeSkip + 1, player.PlayCount);
        }

        [Fact]
        public void Skip_while_not_focusing_loads_but_does_not_play()
        {
            (RadioModel model, RecordingRadioPlayer player, _) = Build(Enabled());

            model.Skip();

            Assert.Equal(RadioStations.All[1].StreamUri, player.Loaded[^1]);
            Assert.Equal(0, player.PlayCount);
        }

        [Theory]
        [InlineData(-0.5, 0.0)]
        [InlineData(1.5, 1.0)]
        [InlineData(0.3, 0.3)]
        public void SetVolume_clamps_and_persists(double input, double expected)
        {
            (RadioModel model, RecordingRadioPlayer player, SettingsService settings) = Build(new AppSettings());

            model.SetVolume(input);

            Assert.Equal(expected, model.Volume);
            Assert.Equal(expected, player.LastVolume);
            Assert.Equal(expected, settings.Current.RadioVolume);
        }

        [Fact]
        public void Constructor_restores_persisted_station_and_volume()
        {
            AppSettings seed = new AppSettings { RadioStationIndex = 2, RadioVolume = 0.8 };
            (RadioModel model, RecordingRadioPlayer player, _) = Build(seed);

            Assert.Equal(RadioStations.All[2].Name, model.CurrentStation.Name);
            Assert.Equal(0.8, model.Volume);
            Assert.Equal(0.8, player.LastVolume);
        }

        [Fact]
        public void Constructor_falls_back_to_first_station_for_out_of_range_index()
        {
            AppSettings seed = new AppSettings { RadioStationIndex = 99 };
            (RadioModel model, _, _) = Build(seed);

            Assert.Equal(RadioStations.All[0].Name, model.CurrentStation.Name);
        }
    }
}
