using System.Media;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private static readonly SolidColorBrush FocusBackgroundBrush = Frozen(FocusBackgroundColor);

        // One frozen brush per mode, built once — Render runs every second and must not allocate brushes.
        private static readonly Dictionary<TimerMode, SolidColorBrush> ModeBackgrounds = BuildModeBackgrounds();

        private static Dictionary<TimerMode, SolidColorBrush> BuildModeBackgrounds()
        {
            Dictionary<TimerMode, SolidColorBrush> brushes = new Dictionary<TimerMode, SolidColorBrush>();
            foreach (TimerMode mode in Enum.GetValues<TimerMode>())
            {
                brushes[mode] = Frozen(ModeTheme.For(mode).Background);
            }

            return brushes;
        }

        private static SolidColorBrush Frozen(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private readonly SettingsService settings = new SettingsService(new SettingsStore());
        private readonly AutoStartManager autoStartManager = new AutoStartManager();
        private readonly ITaskGateway todoistGateway = new HttpTodoistGateway();
        private readonly ITaskGateway clickUpGateway = new HttpClickUpGateway();
        private readonly ITaskGateway asanaGateway = new NullTaskGateway();
        private readonly ITaskGateway gateway;
        private readonly ISessionLog sessionLog = new JsonSessionLog();
        private readonly UpdateChecker updateChecker = new UpdateChecker(new HttpClient());
        private readonly TaskListModel taskList;
        private readonly PomodoroSession session;
        private readonly IFocusBlocker focusBlocker;
        private readonly FocusGuard focusGuard;
        private readonly SessionController controller;
        private readonly IRadioPlayer radioPlayer = new MediaPlayerRadio();
        private readonly RadioModel radioModel;

        // The faux-beat equalizer animation; runs only while the radio is actually playing.
        private readonly Storyboard equalizerBeat;
        private bool isEqualizerRunning;

        // The undo window for task completion; runs on its own clock so a paused timer doesn't stall it.
        private readonly PendingCompletions pendingCompletions =
            new PendingCompletions(new DispatcherClock(), CompletionDelaySeconds);

        private bool isPopulatingProjects;

        // Suppresses the volume slider's ValueChanged while RenderRadio sets it programmatically.
        private bool isSettingVolume;

        // Global start/pause hotkey: Ctrl+Alt+P, works even when the widget isn't focused.
        private const int HotkeyId = 0x9001;
        private const int WmHotkey = 0x0312;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModNoRepeat = 0x4000;
        private const uint VkP = 0x50;
        private IntPtr windowHandle;

        public MainWindow()
        {
            InitializeComponent();

            gateway = new TaskGatewayRouter(settings, todoistGateway, clickUpGateway, asanaGateway);
            taskList = new TaskListModel(gateway, settings);
            session = new PomodoroSession(settings.Current, new DispatcherClock(), sessionLog);

            focusBlocker = new CompositeFocusBlocker(
                new HostsFileBlocker(() => settings.Current.ActiveBlockedHostList()),
                new ProcessBlocker(new DispatcherClock(), new ProcessKiller(), () => settings.Current.ActiveBlockedProcessList()));
            focusGuard = new FocusGuard(focusBlocker, () => settings.Current.BlockDistractionsEnabled);

            controller = new SessionController(
                session, taskList, pendingCompletions, focusGuard, settings, gateway, autoStartManager);

            equalizerBeat = (Storyboard)FindResource("EqualizerBeat");

            radioModel = new RadioModel(radioPlayer, settings);
            radioModel.Changed += RenderRadio;

            session.Changed += Render;
            session.Changed += SyncRadioToFocus;
            controller.Finished += OnSessionFinished;
            taskList.HintChanged += () => ShowHint(taskList.Hint);

            TaskItems.ItemsSource = taskList.Tasks;
            ProjectSelector.ItemsSource = taskList.Projects;

            ApplyWindowPosition();
            autoStartManager.Apply(settings.Current.StartWithWindows);
            ConfigureGateways();

            // Clear any block left in the hosts file by a previous crash before the first focus run.
            focusBlocker.Unblock();

            Render();
            RenderStreak();
            RenderRadio();
            UpdateSourceUi();
            ShowHint(taskList.Hint);

            Loaded += async (_, _) =>
            {
                await SyncAsync();
                await CheckForUpdateAsync();
            };
        }

        private async Task CheckForUpdateAsync()
        {
            string current = GetType().Assembly.GetName().Version?.ToString() ?? "0.0.0";
            string? newer = await updateChecker.LatestNewerThanAsync(current);
            if (newer is null)
            {
                return;
            }

            ToastWindow toast = new ToastWindow($"Update available: {newer}\ngithub.com/szejkerek/Pomodoro/releases") { Owner = this };
            toast.Show();
        }

        private void ConfigureGateways()
        {
            gateway.Configure(settings.Current);
        }

        // ---- Global hotkey ----

        protected override void OnSourceInitialized(EventArgs eventArgs)
        {
            base.OnSourceInitialized(eventArgs);
            windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(windowHandle)?.AddHook(OnWindowMessage);
            RegisterHotKey(windowHandle, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkP);
        }

        private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
            {
                controller.ToggleStartPause();
                handled = true;
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr handle, int id);

        // ---- Timer controls (delegate to the session) ----

        private void OnStartClick(object sender, RoutedEventArgs eventArgs)
        {
            controller.ToggleStartPause();
        }

        private void OnResetClick(object sender, RoutedEventArgs eventArgs)
        {
            controller.Reset();
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

            controller.SwitchMode(mode);
        }

        private void OnSessionFinished(string message)
        {
            if (settings.Current.SoundEnabled)
            {
                SystemSounds.Asterisk.Play();
            }

            // The streak only changes when a session finishes, so recompute here rather than every tick.
            RenderStreak();

            ToastWindow toast = new ToastWindow(message) { Owner = this };
            toast.Show();
        }

        private void OnHeatmapClick(object sender, RoutedEventArgs eventArgs)
        {
            StatsWindow dialog = new StatsWindow(sessionLog) { Owner = this };
            dialog.ShowDialog();
        }

        // ---- Focus radio ----

        private void OnRadioMuteClick(object sender, RoutedEventArgs eventArgs)
        {
            radioModel.ToggleMute();
        }

        private void OnRadioSkipClick(object sender, RoutedEventArgs eventArgs)
        {
            radioModel.Skip();
        }

        private void OnRadioVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> eventArgs)
        {
            if (isSettingVolume)
            {
                return;
            }

            radioModel.SetVolume(eventArgs.NewValue);
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

            await controller.ApplySettingsAsync();
            RestoreProjectSelection();
            UpdateSourceUi();
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
            await controller.SyncAsync();
            RestoreProjectSelection();
            UpdateSourceUi();
        }

        private void UpdateSourceUi()
        {
            StyleTab(SourceTodoist, taskList.ActiveSource == TaskSource.Todoist);
            StyleTab(SourceClickUp, taskList.ActiveSource == TaskSource.ClickUp);
            StyleTab(SourceAsana, taskList.ActiveSource == TaskSource.Asana);
            ProjectSelector.Visibility = taskList.HasProjects ? Visibility.Visible : Visibility.Collapsed;
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
                await controller.FocusTaskAsync(taskId);
            }
        }

        private void OnTaskCircleClick(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is FrameworkElement element && element.Tag is string taskId)
            {
                controller.ToggleCompletion(taskId);
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
            bool isFocusMode = session.IsRunning;
            SolidColorBrush modeBrush = ModeBackgrounds[session.CurrentMode];

            RootBorder.Background = isFocusMode ? FocusBackgroundBrush : modeBrush;
            StartButton.Foreground = isFocusMode ? FocusBackgroundBrush : modeBrush;

            StyleTab(TabPomodoro, session.CurrentMode == TimerMode.Pomodoro);
            StyleTab(TabShort, session.CurrentMode == TimerMode.ShortBreak);
            StyleTab(TabLong, session.CurrentMode == TimerMode.LongBreak);

            ApplyFocusMode(isFocusMode);

            int minutes = session.RemainingSeconds / SecondsPerMinute;
            int seconds = session.RemainingSeconds % SecondsPerMinute;
            TimeText.Text = $"{minutes:00}:{seconds:00}";
            StartButton.Content = isFocusMode ? "PAUSE" : "START";
            Title = $"{TimeText.Text} · Pomodoro";
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

        // Focus radio runs only while a pomodoro is actually in progress — not during breaks.
        private void SyncRadioToFocus()
        {
            bool isFocusRunning = session.IsRunning && session.CurrentMode == TimerMode.Pomodoro;
            radioModel.FollowFocus(isFocusRunning);
        }

        private void RenderRadio()
        {
            RadioPanel.Visibility = radioModel.IsActive ? Visibility.Visible : Visibility.Collapsed;

            SoundWaves.Visibility = radioModel.IsMuted ? Visibility.Collapsed : Visibility.Visible;
            MuteSlash.Visibility = radioModel.IsMuted ? Visibility.Visible : Visibility.Collapsed;
            Equalizer.ToolTip = radioModel.CurrentStation.Name;

            isSettingVolume = true;
            RadioVolumeSlider.Value = radioModel.Volume;
            isSettingVolume = false;

            UpdateEqualizerAnimation(radioModel.IsPlaying);
        }

        // The bars bounce only while audible — start the looping storyboard on play, freeze it on pause.
        private void UpdateEqualizerAnimation(bool shouldAnimate)
        {
            if (shouldAnimate == isEqualizerRunning)
            {
                return;
            }

            isEqualizerRunning = shouldAnimate;
            if (shouldAnimate)
            {
                equalizerBeat.Begin(this, isControllable: true);
                return;
            }

            equalizerBeat.Stop(this);
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
            if (windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(windowHandle, HotkeyId);
            }

            // Never leave the user blocked after the widget closes.
            focusBlocker.Unblock();

            settings.Update(current =>
            {
                current.WindowLeft = Left;
                current.WindowTop = Top;
            });
            base.OnClosing(eventArgs);
        }
    }
}
