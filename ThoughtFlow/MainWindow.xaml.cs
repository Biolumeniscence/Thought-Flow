using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;
    private static readonly Color DefaultInkColor = Color.FromRgb(35, 35, 35);
    private static readonly Color DefaultMarkColor = Color.FromRgb(216, 180, 254);
    private static readonly Color SpoilerHiddenColor = Color.FromRgb(49, 49, 54);
    private static readonly Color SpoilerOpenColor = Color.FromRgb(229, 231, 235);
    private static readonly AppThemeOption[] ThemeOptions = AppThemes.Options;

    private readonly ObservableCollection<FlowChannel> _channels = [];
    private readonly ObservableCollection<FlowTextFile> _files = [];
    private readonly List<FlowMessage> _visibleMessages = [];
    private readonly Dictionary<Paragraph, FlowMessage> _streamParagraphMessages = [];
    private readonly HashSet<string> _openSpoilers = [];
    private readonly FlowStorage _storage = new();
    private FlowAppSettings _settings = new();
    private AppThemeOption _currentTheme = ThemeOptions[0];
    private Color _selectedMarkColor = DefaultMarkColor;
    private Color _currentInkColor = DefaultInkColor;

    private FlowChannel? _activeChannel;
    private FlowTextFile? _activeFile;
    private FlowMessage? _activeMessage;
    private Paragraph? _hoveredStreamParagraph;
    private FlowMessage? _hoveredMessage;
    private FlowMessage? _messageActionsTargetMessage;
    private FlowMessage? _messageContextTargetMessage;
    private bool _messageActionsMenuOpen;
    private bool _isLoadingEditor;
    private bool _hasUnsavedEditorText;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        ApplyContextMenuStyle(MessageStreamBox.ContextMenu);
        ApplyContextMenuStyle(MessageActionsButton.ContextMenu);
        StoragePathText.Text = _storage.DisplayPath;

        LoadLibrary();
        ApplyAppSettings();
        ChannelsList.ItemsSource = _channels;
        FilesList.ItemsSource = _files;
        ChannelsList.SelectedIndex = 0;
    }

}

