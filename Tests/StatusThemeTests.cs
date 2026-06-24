using System.Windows.Media;
using Pomodoro.Presentation;
using Xunit;

namespace Pomodoro.Tests
{
    public class StatusThemeTests
    {
        [Theory]
        [InlineData("in progress")]
        [InlineData("In Progress")]
        [InlineData("actively in progress")]
        public void In_progress_names_share_one_colour(string status)
        {
            Assert.Equal(StatusTheme.InProgress, StatusTheme.For(status));
        }

        [Theory]
        [InlineData("to do", "in progress")]
        [InlineData("in review", "done")]
        [InlineData("in progress", "done")]
        public void Different_workflow_columns_get_different_colours(string first, string second)
        {
            Assert.NotEqual(StatusTheme.For(first), StatusTheme.For(second));
        }

        [Fact]
        public void An_unknown_or_empty_status_falls_back_to_the_to_do_colour()
        {
            Assert.Equal(StatusTheme.ToDo, StatusTheme.For(""));
            Assert.Equal(StatusTheme.ToDo, StatusTheme.For("backlog"));
        }
    }
}
