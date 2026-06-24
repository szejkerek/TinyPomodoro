using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Pomodoro.Presentation
{
    /// <summary>Binds a task's status name to the brush for its workflow column (see <see cref="StatusTheme"/>).</summary>
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string ?? string.Empty;
            return new SolidColorBrush(StatusTheme.For(status));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
