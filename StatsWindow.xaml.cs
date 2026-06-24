using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pomodoro.Models;
using Pomodoro.Presentation;
using Pomodoro.Services;

namespace Pomodoro
{
    /// <summary>Read-only stats dialog: streak, totals, and a day×hour focus heatmap over the session log.</summary>
    public partial class StatsWindow : Window
    {
        private const int HoursPerDay = 24;
        private const double CellSize = 15.0;
        private const double CellGap = 2.0;
        private const double LabelColumnWidth = 34.0;

        // Monday-first week: the value is the DayOfWeek int (Sunday = 0) each row maps to.
        private static readonly (string Label, int DayOfWeekIndex)[] WeekRows =
        {
            ("Mon", 1), ("Tue", 2), ("Wed", 3), ("Thu", 4), ("Fri", 5), ("Sat", 6), ("Sun", 0)
        };

        // Each source's cell is dimmest at this alpha and ramps to fully opaque at the busiest hour.
        private const byte MinHeatAlpha = 0x40;
        private const byte EmptyCellAlpha = 0x1A;

        public StatsWindow(ISessionLog sessionLog)
        {
            InitializeComponent();

            IReadOnlyList<CompletedPomodoro> entries = sessionLog.All();
            RenderSummary(entries);
            RenderHeatmap(SessionStats.WeeklySourceHeatmap(entries));
            RenderLegend();
        }

        private void RenderSummary(IReadOnlyList<CompletedPomodoro> entries)
        {
            DateTime today = DateTime.Now;
            int streak = SessionStats.CurrentStreak(entries, today);
            int total = entries.Count;
            int todayCount = entries.Count(entry => entry.CompletedAt.Date == today.Date);

            SummaryText.Text = $"🔥 Streak: {streak}    •    Today: {todayCount}    •    Total: {total} pomodoros";
        }

        private void RenderHeatmap(int[,,] grid)
        {
            int peak = SessionStats.Peak(grid);

            HeatmapGrid.RowDefinitions.Clear();
            HeatmapGrid.ColumnDefinitions.Clear();
            HeatmapGrid.Children.Clear();

            BuildColumns();
            BuildHourHeaderRow();
            BuildDayRows(grid, peak);
        }

        private void BuildColumns()
        {
            HeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelColumnWidth) });
            for (int hour = 0; hour < HoursPerDay; hour++)
            {
                HeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellSize + CellGap) });
            }
        }

        private void BuildHourHeaderRow()
        {
            HeatmapGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int hour = 0; hour < HoursPerDay; hour += 3)
            {
                TextBlock label = new TextBlock
                {
                    Text = hour.ToString("00"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetRow(label, 0);
                Grid.SetColumn(label, hour + 1);
                HeatmapGrid.Children.Add(label);
            }
        }

        private void BuildDayRows(int[,,] grid, int peak)
        {
            int sourceCount = grid.GetLength(2);
            for (int row = 0; row < WeekRows.Length; row++)
            {
                HeatmapGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                int gridRow = row + 1;

                AddDayLabel(WeekRows[row].Label, gridRow);

                int dayIndex = WeekRows[row].DayOfWeekIndex;
                for (int hour = 0; hour < HoursPerDay; hour++)
                {
                    int[] sourceCounts = new int[sourceCount];
                    for (int source = 0; source < sourceCount; source++)
                    {
                        sourceCounts[source] = grid[dayIndex, hour, source];
                    }

                    HeatmapGrid.Children.Add(BuildCell(sourceCounts, peak, gridRow, hour + 1));
                }
            }
        }

        private void AddDayLabel(string text, int gridRow)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(label, gridRow);
            Grid.SetColumn(label, 0);
            HeatmapGrid.Children.Add(label);
        }

        private Border BuildCell(int[] sourceCounts, int peak, int gridRow, int gridColumn)
        {
            int total = sourceCounts.Sum();
            Border cell = new Border
            {
                Width = CellSize,
                Height = CellSize,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, CellGap, CellGap, 0),
                ClipToBounds = true,
                ToolTip = total == 0 ? null : CellTooltip(sourceCounts)
            };

            if (total == 0 || peak == 0)
            {
                cell.Background = new SolidColorBrush(Color.FromArgb(EmptyCellAlpha, 0xFF, 0xFF, 0xFF));
            }
            else
            {
                // A mixed hour is split into stripes proportional to each context's count, so
                // life + hobby in the same block shows as both colours instead of just the winner.
                cell.Child = BuildStripes(sourceCounts, total, peak);
            }

            Grid.SetRow(cell, gridRow);
            Grid.SetColumn(cell, gridColumn);
            return cell;
        }

        private static Grid BuildStripes(int[] sourceCounts, int total, int peak)
        {
            double intensity = (double)total / peak;
            byte alpha = (byte)(MinHeatAlpha + intensity * (0xFF - MinHeatAlpha));

            Grid stripes = new Grid();
            for (int source = 0; source < sourceCounts.Length; source++)
            {
                if (sourceCounts[source] == 0)
                {
                    continue;
                }

                int column = stripes.ColumnDefinitions.Count;
                stripes.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(sourceCounts[source], GridUnitType.Star)
                });

                Color color = SourceTheme.For((TaskSource)source).Color;
                Border stripe = new Border { Background = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)) };
                Grid.SetColumn(stripe, column);
                stripes.Children.Add(stripe);
            }

            return stripes;
        }

        private static string CellTooltip(int[] sourceCounts)
        {
            IEnumerable<string> parts = sourceCounts
                .Select((count, source) => (count, source))
                .Where(item => item.count > 0)
                .Select(item => $"{SourceTheme.For((TaskSource)item.source).Label}: {item.count}");

            return string.Join("\n", parts);
        }

        private void RenderLegend()
        {
            LegendPanel.Children.Clear();
            foreach (TaskSource source in Enum.GetValues<TaskSource>())
            {
                LegendPanel.Children.Add(BuildLegendEntry(SourceTheme.For(source)));
            }
        }

        private static StackPanel BuildLegendEntry(SourcePalette palette)
        {
            StackPanel entry = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 14, 0)
            };
            entry.Children.Add(new Border
            {
                Width = 11,
                Height = 11,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(palette.Color),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            entry.Children.Add(new TextBlock
            {
                Text = palette.Label,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                FontSize = 10
            });
            return entry;
        }

    }
}
