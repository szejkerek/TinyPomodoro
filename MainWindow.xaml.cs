using System.Linq;
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
        private const int SecondsPerMinute = 60;
        private const double TabActiveOpacity = 1.0;
        private const double TabInactiveOpacity = 0.6;

        // After clicking a task's circle, hold the completion for this long so a misclick can be undone.
        // Must match the fade-out duration of the row in MainWindow.xaml.
        private const int CompletionDelaySeconds = 2;

        private static readonly SolidColorBrush ActiveTabBrush =
            new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        // Focus mode: the widget goes near-black and strips to just the banner + timer while running.
        private static readonly Color FocusBackgroundColor = Color.FromRgb(0x0D, 0x0D, 0x0D);
        private static readonly SolidColorBrush FocusBackgroundBrush = new SolidColorBrush(FocusBackgroundColor);

        private readonly SettingsService settings = new SettingsService(new SettingsStore());
        private readonly AutoStartManager autoStartManager = new AutoStartManager();
        private readonly ITaskGateway todoistGateway = new HttpTodoistGateway();
        private readonly ITaskGateway clickUpGateway = new HttpClickUpGateway();
        private readonly ITaskGateway asanaGateway = new NullTaskGateway();
        private readonly ITaskGateway gateway;
        private readonly ISessionLog sessionLog = new JsonSessionLog();
        private readonly TaskListModel taskList;
        private readonly PomodoroSession session;

        // The undo window for task completion; runs on its own clock so a paused timer doesn't stall it.
        private readonly PendingCompletions pendingCompletions =
            new PendingCompletions(new DispatcherClock(), CompletionDelaySeconds);

        private bool isPopulatingProjects;

        public MainWindow()
        {
            InitializeComponent();

            gateway = new TaskGatewayRouter(settings, todoistGateway, clickUpGateway, asanaGateway);
            taskList = new TaskListModel(gateway, settings);
            session = new PomodoroSession(settings.Current, new DispatcherClock(), sessionLog);

            session.Changed += Render;
            session.Finished += OnSessionFinished;
            taskList.HintChanged += () => ShowHint(taskList.Hint);
            pendingCompletions.Elapsed += taskId => _ = CompletePendingTaskAsync(taskId);

            TaskItems.ItemsSource = taskList.Tasks;
            ProjectSelector.ItemsSource = taskList.Projects;

            ApplyWindowPosition();
            autoStartManager.Apply(settings.Current.StartWithWindows);
            ConfigureGateways();

            Render();
            UpdateSourceUi();
            ShowHint(taskList.Hint);

            Loaded += async (_, _) => await SyncAsync();
        }

        private void ConfigureGateways()
        {
            gateway.Configure(settings.Current);
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

        private void OnHeatmapClick(object sender, RoutedEventArgs eventArgs)
        {
            StatsWindow dialog = new StatsWindow(sessionLog) { Owner = this };
            dialog.ShowDialog();
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
            ConfigureGateways();
            session.ApplySettings();

            await SyncAsync();
        }

        private async void OnSourceTodoistClick(object sender, RoutedEventArgs eventArgs)
        {
            await taskList.SwitchSourceAsync(TaskSource.Todoist);
            RestoreProjectSelection();
            UpdateSourceUi();
        }

        private async void OnSourceClickUpClick(object sender, RoutedEventArgs eventArgs)
        {
            await taskList.SwitchSourceAsync(TaskSource.ClickUp);
            RestoreProjectSelection();
            UpdateSourceUi();
        }

        private async void OnSourceAsanaClick(object sender, RoutedEventArgs eventArgs)
        {
            await taskList.SwitchSourceAsync(TaskSource.Asana);
            RestoreProjectSelection();
            UpdateSourceUi();
        }

        // ---- Todoist (delegate to the task list model) ----

        private async void OnSyncClick(object sender, RoutedEventArgs eventArgs)
        {
            await SyncAsync();
        }

        private async Task SyncAsync()
        {
            ClearPendingCompletions();
            await taskList.SyncAsync();
            RestoreProjectSelection();
            UpdateSourceUi();
        }

        private void UpdateSourceUi()
        {
            StyleTab(SourceTodoist, taskList.ActiveSource == TaskSource.Todoist);
            StyleTab(SourceClickUp, taskList.ActiveSource == TaskSource.ClickUp);
            StyleTab(SourceAsana, taskList.ActiveSource == TaskSource.Asana);
            ProjectSelector.Visibility = taskList.SupportsProjects ? Visibility.Visible : Visibility.Collapsed;
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

        private async void OnTaskRowClick(object sender, MouseButtonEventArgs eventArgs)
        {
            // Handle it here so the click doesn't bubble up to the window's DragMove.
            eventArgs.Handled = true;
            if (sender is FrameworkElement element && element.Tag is string taskId)
            {
                // Focusing a task is also the quick way out of an accidental completion.
                CancelPendingCompletion(taskId);
                await taskList.FocusAsync(taskId);
            }
        }

        private void OnTaskCircleClick(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is FrameworkElement element && element.Tag is string taskId)
            {
                if (pendingCompletions.IsPending(taskId))
                {
                    CancelPendingCompletion(taskId);
                    return;
                }

                BeginPendingCompletion(taskId);
            }
        }

        private void BeginPendingCompletion(string taskId)
        {
            TodoistTask? task = taskList.Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task is null)
            {
                return;
            }

            task.IsCompleting = true;
            pendingCompletions.Begin(taskId);
        }

        private void CancelPendingCompletion(string taskId)
        {
            pendingCompletions.Cancel(taskId);

            TodoistTask? task = taskList.Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task is not null)
            {
                task.IsCompleting = false;
            }
        }

        private async Task CompletePendingTaskAsync(string taskId)
        {
            await taskList.CloseTaskAsync(taskId);
        }

        private void ClearPendingCompletions()
        {
            pendingCompletions.ClearAll();
        }

        private void ShowHint(string message)
        {
            TasksHint.Text = message;
            TasksHint.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // ---- Rendering ----

        private void Render()
        {
            bool isFocusMode = session.IsRunning;
            Color modeColor = ModeTheme.For(session.CurrentMode).Background;

            RootBorder.Background = isFocusMode ? FocusBackgroundBrush : new SolidColorBrush(modeColor);
            StartButton.Foreground = new SolidColorBrush(isFocusMode ? FocusBackgroundColor : modeColor);

            StyleTab(TabPomodoro, session.CurrentMode == TimerMode.Pomodoro);
            StyleTab(TabShort, session.CurrentMode == TimerMode.ShortBreak);
            StyleTab(TabLong, session.CurrentMode == TimerMode.LongBreak);

            ApplyFocusMode(isFocusMode);

            int minutes = session.RemainingSeconds / SecondsPerMinute;
            int seconds = session.RemainingSeconds % SecondsPerMinute;
            TimeText.Text = $"{minutes:00}:{seconds:00}";
            StartButton.Content = isFocusMode ? "PAUSE" : "START";
            Title = $"{TimeText.Text} · Pomodoro";

            RenderStreak();
        }

        private void ApplyFocusMode(bool isFocusMode)
        {
            // The mode tabs and chrome go away (switching is blocked while running anyway),
            // but the task list stays visible so you can see what to work on.
            Visibility chromeVisibility = isFocusMode ? Visibility.Collapsed : Visibility.Visible;
            TopRow.Visibility = chromeVisibility;
            HeatmapButton.Visibility = chromeVisibility;

            FocusLabel.Visibility = isFocusMode ? Visibility.Visible : Visibility.Collapsed;
            FocusLabel.Text = session.CurrentMode == TimerMode.Pomodoro ? "FOCUS MODE" : "BREAK";
        }

        private void RenderStreak()
        {
            int streak = SessionStats.CurrentStreak(sessionLog.All(), DateTime.Now);
            HeatmapButton.Content = streak == 0 ? "🔥" : $"🔥 {streak}";
            HeatmapButton.ToolTip = streak == 0 ? "Focus stats" : $"Focus stats · {streak}-day streak";
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
