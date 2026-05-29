using System.Windows.Media;

namespace ThoughtFlow;

public static class MarkerPalette
{
    private static readonly Dictionary<Color, Color> MutedByBase = new()
    {
        [Color.FromRgb(216, 180, 254)] = Color.FromRgb(103, 79, 132),
        [Color.FromRgb(191, 219, 254)] = Color.FromRgb(76, 100, 128),
        [Color.FromRgb(187, 247, 208)] = Color.FromRgb(73, 110, 87),
        [Color.FromRgb(254, 240, 138)] = Color.FromRgb(126, 112, 54),
        [Color.FromRgb(254, 215, 170)] = Color.FromRgb(126, 92, 65),
        [Color.FromRgb(251, 207, 232)] = Color.FromRgb(126, 82, 106),
        [Color.FromRgb(207, 250, 254)] = Color.FromRgb(73, 117, 122),
        [Color.FromRgb(229, 231, 235)] = Color.FromRgb(96, 101, 108)
    };

    private static readonly Dictionary<Color, Color> BaseByMuted = MutedByBase.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static Color ToDisplayColor(Color color, bool useMutedColors)
    {
        if (useMutedColors)
        {
            return MutedByBase.TryGetValue(color, out var muted) ? muted : MutColor(color);
        }

        return BaseByMuted.TryGetValue(color, out var baseColor) ? baseColor : color;
    }

    public static Color ToStorageColor(Color color)
    {
        return BaseByMuted.TryGetValue(color, out var baseColor) ? baseColor : color;
    }

    private static Color MutColor(Color color)
    {
        const double mix = 0.42;
        var target = Color.FromRgb(62, 64, 68);
        return Color.FromArgb(
            color.A,
            MixChannel(color.R, target.R, mix),
            MixChannel(color.G, target.G, mix),
            MixChannel(color.B, target.B, mix));
    }

    private static byte MixChannel(byte source, byte target, double targetAmount)
    {
        return (byte)Math.Round(source * (1 - targetAmount) + target * targetAmount);
    }
}
