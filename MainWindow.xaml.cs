using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Pomodoro.Models;
using Pomodoro.Presentation;
using Pomodoro.Services;

namespace Pomodoro
{
    public partial class MainWindow : Window
    {
        private const double TabActiveOpacity = 1.0;
        private const double TabInactiveOpacity = 0.6;

        private static readonly SolidColorBrush ActiveTabBrush =
            new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        private readonly SettingsService settings = new SettingsService(new SettingsStore());
        private readonly AutoStartManager autoStartManager = new AutoStartManager();
        private readonly ITodoistGateway gateway = new HttpTodoistGateway();
        private readonly TaskListModel taskList;
        private readonly PomodoroSession session;

        private bool isPopulatingProjects;

        public MainWindow()
        {
            InitializeComponent();

            taskList = new TaskListModel(gateway, settings);
            session = new PomodoroSession(settings.Current, new DispatcherClock());

            session.Changed += Render;
            session.Finished += OnSessionFinished;
            taskList.HintChanged += () => ShowHint(taskList.Hint);

            TaskItems.ItemsSource = taskList.Tasks;
            ProjectSelector.ItemsSource = taskList.Projects;

            ApplyWindowPosition();
            autoStartManager.Apply(settings.Current.StartWithWindows);

            Render();
            ShowHint(taskList.Hint);

            if (settings.Current.TodoistToken.Length > 0)
            {
                gateway.UseToken(settings.Current.TodoistToken);
                Loaded += async (_, _) => await SyncAsync();
            }
        }

        // ---- Timer controls (delegate to the session) ----

        private void OnStartClick(object sender, RoutedEventArgs eventArgs)
        {
            session.ToggleStartPause();
        }

        private void OnResetClick(object sender, RoutedEventArgs eventArgs)
        {
            session.Reset();
        }

        private void OnModeTabClick(object sender, RoutedEventArgs eventArgs)
        {
            TimerMode mode = TimerMode.Pomodoro;
            if (sender == TabShort)
            {
                mode = TimerMode.ShortBreak;
            }
            else if (sender == TabLong)
            {
                mode = TimerMode.LongBreak;
            }

            session.SwitchTo(mode);
        }

        private void OnSessionFinished()
        {
            if (settings.Current.SoundEnabled)
            {
                SystemSounds.Asterisk.Play();
            }
        }

        // ---- Window chrome ----

        private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
        {
            DragMove();
        }

        private async void OnSettingsClick(object sender, RoutedEventArgs eventArgs)
        {
            SettingsWindow dialog = new SettingsWindow(settings.Current) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            settings.Save();
            autoStartManager.Apply(settings.Current.StartWithWindows);
            gateway.UseToken(settings.Current.TodoistToken);
            session.ApplySettings();

            await SyncAsync();
        }

        // ---- Todoist (delegate to the task list model) ----

        private async void OnSyncClick(object sender, RoutedEventArgs eventArgs)
        {
            await SyncAsync();
        }

        private async Task SyncAsync()
        {
            await taskList.SyncAsync();
            RestoreProjectSelection();
        }

        private void RestoreProjectSelection()
        {
            isPopulatingProjects = true;
            ProjectSelector.SelectedValue = taskList.SelectedProjectId;
            if (ProjectSelector.SelectedItem is null && ProjectSelector.Items.Count > 0)
            {
                ProjectSelector.SelectedIndex = 0;
            }

            isPopulatingProjects = false;
        }

        private async void OnProjectChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            if (isPopulatingProjects)
            {
                return;
            }

            await taskList.SelectProjectAsync(ProjectSelector.SelectedValue as string ?? string.Empty);
        }

        private async void OnTaskClick(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is FrameworkElement element && element.Tag is string taskId)
            {
                await taskList.CloseTaskAsync(taskId);
            }
        }

        private void ShowHint(string message)
        {
            TasksHint.Text = message;
            TasksHint.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // ---- Rendering ----

        private void Render()
        {
            Color modeColor = ModeTheme.For(session.CurrentMode).Background;
            RootBorder.Background = new SolidColorBrush(modeColor);
            StartButton.Foreground = new SolidColorBrush(modeColor);

            StyleTab(TabPomodoro, session.CurrentMode == TimerMode.Pomodoro);
            StyleTab(TabShort, session.CurrentMode == TimerMode.ShortBreak);
            StyleTab(TabLong, session.CurrentMode == TimerMode.LongBreak);

            int minutes = session.RemainingSeconds / 60;
            int seconds = session.RemainingSeconds % 60;
            TimeText.Text = $"{minutes:00}:{seconds:00}";
            StartButton.Content = session.IsRunning ? "PAUZA" : "START";
            Title = $"{TimeText.Text} · Pomodoro";
        }

        private static void StyleTab(Button tab, bool isActive)
        {
            tab.Opacity = isActive ? TabActiveOpacity : TabInactiveOpacity;
            tab.Background = isActive ? ActiveTabBrush : Brushes.Transparent;
            tab.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
        }

        // ---- Window position ----

        private void ApplyWindowPosition()
        {
            if (settings.Current.WindowLeft is null || settings.Current.WindowTop is null)
            {
                Left = SystemParameters.WorkArea.Right - Width - 24;
                Top = 24;
                return;
            }

            Left = settings.Current.WindowLeft.Value;
            Top = settings.Current.WindowTop.Value;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs eventArgs)
        {
            settings.Update(current =>
            {
                current.WindowLeft = Left;
                current.WindowTop = Top;
            });
            base.OnClosing(eventArgs);
        }
    }
}
