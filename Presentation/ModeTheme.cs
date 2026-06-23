using System.Windows.Media;
using Pomodoro.Models;

namespace Pomodoro.Presentation
{
    public readonly struct ModePalette
    {
        public ModePalette(Color background)
        {
            Background = background;
        }

        public Color Background { get; }
    }

    /// <summary>Single source of truth for each mode's look. Deepens the scattered colour mapping.</summary>
    public static class ModeTheme
    {
        // Deepened, cohesive luminance; white text stays above ~4.5:1 contrast.
        private static readonly ModePalette Pomodoro = new ModePalette(Color.FromRgb(0xA8, 0x4B, 0x4B));
        private static readonly ModePalette ShortBreak = new ModePalette(Color.FromRgb(0x37, 0x76, 0x7A));
        private static readonly ModePalette LongBreak = new ModePalette(Color.FromRgb(0x36, 0x5C, 0x84));

        public static ModePalette For(TimerMode mode)
        {
            if (mode == TimerMode.ShortBreak)
            {
                return ShortBreak;
            }

            if (mode == TimerMode.LongBreak)
            {
                return LongBreak;
            }

            return Pomodoro;
        }
    }
}
