using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class DueDateLabelTests
    {
        [Theory]
        [InlineData("2026-06-26")]
        [InlineData("2026-06-26T13:00:00.000000Z")]
        public void A_todoist_date_is_shown_short(string isoDate)
        {
            Assert.Equal("📅 Jun 26", DueDateLabel.FromTodoist(isoDate));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-date")]
        public void A_missing_or_unparseable_todoist_date_shows_nothing(string? isoDate)
        {
            Assert.Equal("", DueDateLabel.FromTodoist(isoDate));
        }

        [Fact]
        public void A_clickup_millisecond_timestamp_is_shown_short()
        {
            long millis = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

            Assert.Equal("📅 Jun 26", DueDateLabel.FromClickUpMillis(millis.ToString()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        public void A_missing_or_zero_clickup_timestamp_shows_nothing(string? millis)
        {
            Assert.Equal("", DueDateLabel.FromClickUpMillis(millis));
        }
    }
}
