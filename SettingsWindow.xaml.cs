using System.Globalization;
using System.Windows;
using Pomodoro.Models;

namespace Pomodoro
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            this.settings = settings;
            LoadFromSettings();
        }

        private void LoadFromSettings()
        {
            PomodoroBox.Text = settings.PomodoroMinutes.ToString(CultureInfo.InvariantCulture);
            ShortBox.Text = settings.ShortBreakMinutes.ToString(CultureInfo.InvariantCulture);
            LongBox.Text = settings.LongBreakMinutes.ToString(CultureInfo.InvariantCulture);
            IntervalBox.Text = settings.LongBreakInterval.ToString(CultureInfo.InvariantCulture);

            AutoBreaksBox.IsChecked = settings.AutoStartBreaks;
            AutoPomodorosBox.IsChecked = settings.AutoStartPomodoros;
            SoundBox.IsChecked = settings.SoundEnabled;
            StartWithWindowsBox.IsChecked = settings.StartWithWindows;

            TokenBox.Password = settings.TodoistToken;
            FilterBox.Text = settings.TodoistFilter;

            ClickUpTokenBox.Password = settings.ClickUpToken;
            ClickUpListBox.Text = settings.ClickUpListId;
        }

        private void OnSaveClick(object sender, RoutedEventArgs eventArgs)
        {
            settings.PomodoroMinutes = ReadMinutes(PomodoroBox.Text, settings.PomodoroMinutes);
            settings.ShortBreakMinutes = ReadMinutes(ShortBox.Text, settings.ShortBreakMinutes);
            settings.LongBreakMinutes = ReadMinutes(LongBox.Text, settings.LongBreakMinutes);
            settings.LongBreakInterval = ReadMinutes(IntervalBox.Text, settings.LongBreakInterval);

            settings.AutoStartBreaks = AutoBreaksBox.IsChecked == true;
            settings.AutoStartPomodoros = AutoPomodorosBox.IsChecked == true;
            settings.SoundEnabled = SoundBox.IsChecked == true;
            settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;

            settings.TodoistToken = TokenBox.Password.Trim();
            settings.TodoistFilter = FilterBox.Text.Trim();

            settings.ClickUpToken = ClickUpTokenBox.Password.Trim();
            settings.ClickUpListId = ClickUpListBox.Text.Trim();

            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs eventArgs)
        {
            DialogResult = false;
        }

        private static int ReadMinutes(string text, int fallback)
        {
            bool isValid = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed);
            if (isValid && parsed > 0)
            {
                return parsed;
            }

            return fallback;
        }
    }
}
