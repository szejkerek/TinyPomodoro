using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class HeatmapPeakTests
    {
        [Fact]
        public void An_empty_grid_has_a_peak_of_zero()
        {
            int[,,] grid = new int[7, 24, 3];

            Assert.Equal(0, SessionStats.Peak(grid));
        }

        [Fact]
        public void The_peak_is_the_busiest_slot_summed_across_sources()
        {
            int[,,] grid = new int[7, 24, 3];
            grid[1, 9, 0] = 2;
            grid[1, 9, 1] = 3; // same slot as above -> total 5
            grid[2, 10, 0] = 4; // a different, smaller slot

            Assert.Equal(5, SessionStats.Peak(grid));
        }
    }
}
