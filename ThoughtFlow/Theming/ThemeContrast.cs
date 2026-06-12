using System.Windows.Media;

namespace ThoughtFlow;

public static class ThemeContrast
{
    public static SolidColorBrush Brush(Color color)
    {
        return new SolidColorBrush(color);
    }

    public static SolidColorBrush ForegroundOn(Color background, AppThemeOption theme)
    {
        var inkContrast = ContrastRatio(background, theme.Ink);
        var sideTextContrast = ContrastRatio(background, theme.SideText);
        var whiteContrast = ContrastRatio(background, Colors.White);

        if (inkContrast >= sideTextContrast && inkContrast >= whiteContrast)
        {
            return Brush(theme.Ink);
        }

        return sideTextContrast >= whiteContrast ? Brush(theme.SideText) : Brushes.White;
    }

    public static Color Mix(Color left, Color right, double rightAmount)
    {
        var clamped = Math.Clamp(rightAmount, 0, 1);
        var leftAmount = 1 - clamped;
        return Color.FromRgb(
            (byte)Math.Round(left.R * leftAmount + right.R * clamped),
            (byte)Math.Round(left.G * leftAmount + right.G * clamped),
            (byte)Math.Round(left.B * leftAmount + right.B * clamped));
    }

    private static double ContrastRatio(Color left, Color right)
    {
        var first = RelativeLuminance(left);
        var second = RelativeLuminance(right);
        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte channel)
        {
            var normalized = channel / 255d;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) +
               0.7152 * Channel(color.G) +
               0.0722 * Channel(color.B);
    }
}
