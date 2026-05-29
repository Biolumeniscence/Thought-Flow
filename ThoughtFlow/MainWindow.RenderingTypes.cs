using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ThoughtFlow;

public partial class MainWindow
{
    [Flags]
    private enum TextRenderStyle
    {
        None = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        Strike = 8,
        Monospace = 16
    }

    private sealed record RenderChar(char Character, int RawIndex, TextRenderStyle Style, string? SpoilerKey);

    private sealed record RenderSegment(string Text, TextRenderStyle Style, string? SpoilerKey, Color? MarkColor);

    private sealed record TextMark(int Start, int End, Color Color);
}

