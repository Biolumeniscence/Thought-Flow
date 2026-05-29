using System.Windows.Media;

namespace ThoughtFlow;

public sealed record AppThemeOption(
    string Id,
    string EnglishName,
    string RussianName,
    string GermanName,
    Color Background,
    Color Panel,
    Color Input,
    Color Line,
    Color Accent,
    Color AccentSoft,
    Color Warm,
    Color Side,
    Color SideText,
    Color SideMuted,
    Color SideDim,
    Color Ink,
    Color Muted,
    Color Ghost,
    Color WorkspaceHover,
    Color WorkspaceSelected,
    Color WorkspaceSelectedText,
    Color FileHoverBorder);

