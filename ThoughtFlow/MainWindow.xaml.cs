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
    private static readonly AppThemeOption[] ThemeOptions =
    [
        new(
            "paper-blue",
            "Paper Blue",
            "Бумажно-синяя",
            "Papierblau",
            Color.FromRgb(243, 240, 234),
            Color.FromRgb(255, 252, 247),
            Color.FromRgb(255, 255, 255),
            Color.FromRgb(216, 208, 196),
            Color.FromRgb(49, 91, 122),
            Color.FromRgb(226, 238, 243),
            Color.FromRgb(169, 95, 71),
            Color.FromRgb(37, 49, 55),
            Color.FromRgb(255, 248, 236),
            Color.FromRgb(184, 196, 201),
            Color.FromRgb(143, 160, 167),
            Color.FromRgb(35, 35, 35),
            Color.FromRgb(108, 104, 97),
            Color.FromRgb(233, 226, 216),
            Color.FromRgb(54, 67, 74),
            Color.FromRgb(230, 240, 244),
            Color.FromRgb(30, 46, 53),
            Color.FromRgb(159, 175, 184)),
        new(
            "graphite",
            "Graphite",
            "Графитовая",
            "Graphit",
            Color.FromRgb(40, 42, 45),
            Color.FromRgb(62, 64, 68),
            Color.FromRgb(72, 74, 79),
            Color.FromRgb(101, 104, 111),
            Color.FromRgb(176, 183, 191),
            Color.FromRgb(82, 86, 92),
            Color.FromRgb(190, 117, 98),
            Color.FromRgb(30, 32, 35),
            Color.FromRgb(239, 241, 244),
            Color.FromRgb(190, 196, 202),
            Color.FromRgb(147, 154, 161),
            Color.FromRgb(244, 244, 245),
            Color.FromRgb(198, 204, 211),
            Color.FromRgb(83, 86, 92),
            Color.FromRgb(55, 58, 62),
            Color.FromRgb(210, 216, 223),
            Color.FromRgb(26, 28, 31),
            Color.FromRgb(138, 145, 153)),
        new(
            "violet-blush",
            "Violet Blush",
            "Фиолетово-розовая",
            "Violettrosa",
            Color.FromRgb(248, 240, 249),
            Color.FromRgb(255, 250, 253),
            Color.FromRgb(255, 255, 255),
            Color.FromRgb(224, 207, 229),
            Color.FromRgb(117, 80, 148),
            Color.FromRgb(241, 225, 247),
            Color.FromRgb(175, 87, 120),
            Color.FromRgb(68, 50, 78),
            Color.FromRgb(255, 246, 251),
            Color.FromRgb(220, 196, 225),
            Color.FromRgb(176, 145, 185),
            Color.FromRgb(49, 37, 58),
            Color.FromRgb(111, 85, 119),
            Color.FromRgb(238, 222, 240),
            Color.FromRgb(87, 64, 100),
            Color.FromRgb(244, 226, 249),
            Color.FromRgb(52, 36, 62),
            Color.FromRgb(183, 150, 190)),
        new(
            "moss",
            "Moss",
            "Моховая",
            "Moos",
            Color.FromRgb(239, 241, 232),
            Color.FromRgb(252, 253, 246),
            Color.FromRgb(255, 255, 252),
            Color.FromRgb(206, 214, 194),
            Color.FromRgb(76, 111, 82),
            Color.FromRgb(224, 235, 216),
            Color.FromRgb(158, 100, 63),
            Color.FromRgb(36, 55, 45),
            Color.FromRgb(247, 250, 239),
            Color.FromRgb(184, 201, 178),
            Color.FromRgb(132, 153, 128),
            Color.FromRgb(31, 45, 37),
            Color.FromRgb(93, 111, 88),
            Color.FromRgb(224, 229, 211),
            Color.FromRgb(49, 70, 56),
            Color.FromRgb(226, 238, 218),
            Color.FromRgb(28, 46, 36),
            Color.FromRgb(142, 163, 137))
    ];

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

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProc);
        }
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo)
        {
            return IntPtr.Zero;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return IntPtr.Zero;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.MonitorArea;

        minMaxInfo.MaxPosition.X = workArea.Left - monitorArea.Left;
        minMaxInfo.MaxPosition.Y = workArea.Top - monitorArea.Top;
        minMaxInfo.MaxSize.X = workArea.Right - workArea.Left;
        minMaxInfo.MaxSize.Y = workArea.Bottom - workArea.Top;

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
        handled = true;
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInfo
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public PointInfo Reserved;
        public PointInfo MaxSize;
        public PointInfo MaxPosition;
        public PointInfo MinTrackSize;
        public PointInfo MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RectInfo MonitorArea;
        public RectInfo WorkArea;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectInfo
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void LoadLibrary()
    {
        var library = _storage.Load();
        library.Normalize();
        _settings = library.Settings;

        if (library.Channels.Count == 0)
        {
            library = FlowLibrary.CreateStarter();
            library.Normalize();
            _settings = library.Settings;
            _storage.Save(library);
        }

        foreach (var channel in library.Channels)
        {
            AttachChannel(channel);
            _channels.Add(channel);
        }
    }

    private void AttachChannel(FlowChannel channel)
    {
        channel.PropertyChanged += (_, _) => SaveLibrary();
        channel.Files.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
            {
                foreach (FlowTextFile file in e.NewItems)
                {
                    AttachFile(file, channel);
                }
            }

            channel.NotifyCountChanged();
            if (channel == _activeChannel)
            {
                RefreshFiles();
                RefreshHeader();
            }

            SaveLibrary();
        };

        foreach (var file in channel.Files)
        {
            AttachFile(file, channel);
        }
    }

    private void AttachFile(FlowTextFile file, FlowChannel channel)
    {
        file.PropertyChanged += (_, _) =>
        {
            channel.NotifyCountChanged();
            SaveLibrary();
            RefreshHeader();
        };

        file.Messages.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
            {
                foreach (FlowMessage message in e.NewItems)
                {
                    AttachMessage(message, file, channel);
                }
            }

            file.NotifyCountChanged();
            channel.NotifyCountChanged();
            SaveLibrary();
            RefreshHeader();

            if (file == _activeFile)
            {
                RefreshMessages();
            }
        };

        foreach (var message in file.Messages)
        {
            AttachMessage(message, file, channel);
        }
    }

    private void AttachMessage(FlowMessage message, FlowTextFile file, FlowChannel channel)
    {
        message.PropertyChanged += (_, _) =>
        {
            file.NotifyCountChanged();
            channel.NotifyCountChanged();
            SaveLibrary();
            RefreshHeader();

            if (file == _activeFile)
            {
                RefreshMessages();
            }
        };
    }

    private FlowLibrary Snapshot()
    {
        return new FlowLibrary { Settings = _settings, Channels = [.. _channels] };
    }

    private void SaveLibrary()
    {
        var snapshot = Snapshot();
        _storage.Save(snapshot);
        var syncError = SaveWorkspaceFiles(snapshot);
        SaveStatusText.Text = syncError is null
            ? $"{L("Saved", "Сохранено", "Gespeichert")} {DateTime.Now:HH:mm:ss}"
            : $"{L("Saved, sync issue", "Сохранено, проблема синхронизации", "Gespeichert, Sync-Problem")}: {syncError}";
    }

    private void ApplyAppSettings()
    {
        _settings.Normalize();
        _currentTheme = ThemeOptions.FirstOrDefault(theme => theme.Id == _settings.Theme) ?? ThemeOptions[0];
        _settings.Theme = _currentTheme.Id;
        ApplyTheme(_currentTheme);
        ApplyLanguage();
        ApplyMarkerSetting();
        UpdateWindowControlLabels();
    }

    private void ApplyTheme(AppThemeOption theme)
    {
        _currentInkColor = theme.Ink;
        SetBrush("AppBackground", theme.Background);
        SetBrush("Ink", theme.Ink);
        SetBrush("MutedInk", theme.Muted);
        SetBrush("Panel", theme.Panel);
        SetBrush("Input", theme.Input);
        SetBrush("Line", theme.Line);
        SetBrush("Accent", theme.Accent);
        SetBrush("AccentSoft", theme.AccentSoft);
        SetBrush("Warm", theme.Warm);
        SetBrush("Side", theme.Side);
        SetBrush("SideText", theme.SideText);
        SetBrush("SideMuted", theme.SideMuted);
        SetBrush("SideDim", theme.SideDim);
        SetBrush("Ghost", theme.Ghost);
        SetBrush("MessageHover", theme.Ghost);
        SetBrush("WorkspaceHover", theme.WorkspaceHover);
        SetBrush("WorkspaceSelected", theme.WorkspaceSelected);
        SetBrush("WorkspaceSelectedText", theme.WorkspaceSelectedText);
        SetBrush("FileHoverBorder", theme.FileHoverBorder);
        Background = new SolidColorBrush(theme.Background);
        RefreshMessages();
    }

    private void SetBrush(string key, Color color)
    {
        if (Resources[key] is SolidColorBrush { IsFrozen: false } brush)
        {
            brush.Color = color;
        }
        else
        {
            Resources[key] = new SolidColorBrush(color);
        }

        if (Application.Current?.Resources is not { } appResources)
        {
            return;
        }

        if (appResources[key] is SolidColorBrush { IsFrozen: false } appBrush)
        {
            appBrush.Color = color;
            return;
        }

        appResources[key] = new SolidColorBrush(color);
    }

    private void ApplyMarkerSetting()
    {
        if (ColorConverter.ConvertFromString(_settings.DefaultMarkerColor) is Color color)
        {
            _selectedMarkColor = color;
            MarkColorSwatch.Background = new SolidColorBrush(color);
        }
    }

    private void ApplyLanguage()
    {
        AppSubtitleText.Text = L("workspaces, files, message-text", "воркспейсы, файлы, текст", "Arbeitsbereiche, Dateien, Text");
        NewWorkspaceLabel.Text = L("New workspace", "Новый воркспейс", "Neuer Arbeitsbereich");
        DuplicateMessageButton.Content = L("Duplicate msg", "Дублировать", "Duplizieren");
        DeleteWorkspaceButton.Content = L("Delete workspace", "Удалить воркспейс", "Arbeitsbereich löschen");
        FilesLabel.Text = L("Files", "Файлы", "Dateien");
        DeleteFileButton.Content = L("Delete file", "Удалить файл", "Datei löschen");
        NewFileLabel.Text = L("New file", "Новый файл", "Neue Datei");
        SendButton.Content = L("Send", "Отправить", "Senden");
        EditorTitleText.Text = L("Message editor", "Редактор сообщения", "Nachrichteneditor");
        MarkButton.ToolTip = L("Mark selected text with the chosen color", "Подсветить выделенный текст выбранным цветом", "Markiert ausgewählten Text mit der gewählten Farbe");
        MarkColorSwatch.ToolTip = L("Choose marker color", "Выбрать цвет маркера", "Markerfarbe wählen");
        MarkButtonText.Text = L("Mark", "Маркер", "Markieren");
        ClearMarkButton.Content = L("Clear", "Очистить", "Leeren");
        ClearMarkButton.ToolTip = L("Clear selected text marker", "Убрать маркер с выделенного текста", "Marker aus ausgewähltem Text entfernen");
        CopyButton.Content = L("Copy", "Копировать", "Kopieren");
        DeleteMessageButton.Content = L("Delete", "Удалить", "Löschen");
        SaveButton.Content = L("Save", "Сохранить", "Speichern");
        SettingsButton.ToolTip = L("Settings", "Настройки", "Einstellungen");
        MessageActionsButton.ToolTip = L("Message actions", "Действия сообщения", "Nachrichtenaktionen");
        UpdateWindowControlLabels();
        StreamEditMenuItem.Header = L("Edit", "Редактировать", "Bearbeiten");
        StreamDuplicateMenuItem.Header = L("Duplicate", "Дублировать", "Duplizieren");
        StreamCopyMenuItem.Header = L("Copy", "Копировать", "Kopieren");
        StreamDeleteMenuItem.Header = L("Delete", "Удалить", "Löschen");
        HoverEditMenuItem.Header = StreamEditMenuItem.Header;
        HoverDuplicateMenuItem.Header = StreamDuplicateMenuItem.Header;
        HoverCopyMenuItem.Header = StreamCopyMenuItem.Header;
        HoverDeleteMenuItem.Header = StreamDeleteMenuItem.Header;
        RefreshHeader();
        RefreshMessages();
        RefreshEditorLabels();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        UpdateWindowControlLabels();
    }

    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateWindowControlLabels()
    {
        MinimizeWindowButton.ToolTip = L("Minimize", "Свернуть", "Minimieren");
        CloseWindowButton.ToolTip = L("Close", "Закрыть", "Schließen");

        var isMaximized = WindowState == WindowState.Maximized;
        MaximizeWindowButton.ToolTip = isMaximized
            ? L("Restore", "Восстановить", "Wiederherstellen")
            : L("Maximize", "Развернуть", "Maximieren");
        MaximizeWindowIcon.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreWindowIcon.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshEditorLabels()
    {
        if (_activeMessage is null)
        {
            EditorHint.Text = L(
                "Click a message in the stream to edit that message here.",
                "Нажми на сообщение в потоке, чтобы редактировать его здесь.",
                "Klicke auf eine Nachricht im Stream, um sie hier zu bearbeiten.");
            EditorStateText.Text = string.Empty;
            return;
        }

        EditorHint.Text = $"{_activeMessage.TimestampText} - {_activeMessage.WordCountText}";
        if (!_hasUnsavedEditorText)
        {
            EditorStateText.Text = L("Saved", "Сохранено", "Gespeichert");
        }
    }

    private string L(string english, string russian)
    {
        return L(english, russian, english);
    }

    private string L(string english, string russian, string german)
    {
        return _settings.Language switch
        {
            "ru" => russian,
            "de" => german,
            _ => english
        };
    }

    private string? SaveWorkspaceFiles(FlowLibrary library)
    {
        string? firstError = null;
        foreach (var workspace in library.Channels)
        {
            if (string.IsNullOrWhiteSpace(workspace.LocationPath))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(workspace.LocationPath);
                var usedNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                foreach (var file in workspace.Files)
                {
                    var fileName = MakeSafeFileName(file.Name, usedNames);
                    var path = Path.Combine(workspace.LocationPath, $"{fileName}.md");
                    File.WriteAllText(path, BuildWorkspaceFileText(file));
                }
            }
            catch (Exception ex)
            {
                firstError ??= ex.Message;
            }
        }

        return firstError;
    }

    private static string CreateWorkspaceFolder(string parentFolder, string workspaceName, IEnumerable<string> reservedWorkspaceNames, out string actualName)
    {
        Directory.CreateDirectory(parentFolder);
        var folderPath = MakeAvailableWorkspaceFolderPath(parentFolder, workspaceName, reservedWorkspaceNames, null, out actualName);
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private static string BuildWorkspaceFileText(FlowTextFile file)
    {
        return string.Join(Environment.NewLine + Environment.NewLine, file.Messages.Select(message => message.Body));
    }

    private static string MakeSafeFileName(string name, HashSet<string> usedNames)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = string.Concat(name.Select(character => invalid.Contains(character) ? '-' : character)).Trim();
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "untitled";
        }

        var candidate = safe;
        for (var i = 2; !usedNames.Add(candidate); i++)
        {
            candidate = $"{safe}-{i}";
        }

        return candidate;
    }

    private static string MakeAvailableWorkspaceFolderPath(
        string parentFolder,
        string workspaceName,
        IEnumerable<string> reservedWorkspaceNames,
        string? currentPath,
        out string actualName)
    {
        var baseName = MakeSafeFolderName(workspaceName);
        var reserved = reservedWorkspaceNames.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var candidateName = baseName;

        for (var i = 2; ; i++)
        {
            var candidatePath = Path.Combine(parentFolder, candidateName);
            var isCurrentPath = currentPath is not null && IsSamePath(candidatePath, currentPath);
            if ((isCurrentPath || (!Directory.Exists(candidatePath) && !File.Exists(candidatePath))) &&
                (isCurrentPath || !reserved.Contains(candidateName)))
            {
                actualName = candidateName;
                return candidatePath;
            }

            candidateName = $"{baseName}-{i}";
        }
    }

    private static string MakeSafeFolderName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = string.Concat(value.Select(character => invalid.Contains(character) ? '-' : character)).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "untitled" : safe;
    }

    private static bool IsSamePath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ChannelsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SaveEditorIfDirty();
        _activeChannel = ChannelsList.SelectedItem as FlowChannel;

        if (_activeChannel is not null && _activeChannel.Files.Count == 0)
        {
            _activeChannel.Files.Add(new FlowTextFile { Name = "main" });
        }

        RefreshFiles();
        var nextFile = _activeChannel?.Files.FirstOrDefault();
        FilesList.SelectedItem = nextFile;
        SelectFile(nextFile);
        RefreshHeader();
    }

    private void RefreshFiles()
    {
        var selectedId = _activeFile?.Id;
        _files.Clear();

        if (_activeChannel is null)
        {
            return;
        }

        foreach (var file in _activeChannel.Files)
        {
            _files.Add(file);
        }

        if (selectedId is not null)
        {
            FilesList.SelectedItem = _files.FirstOrDefault(file => file.Id == selectedId);
        }
    }

    private void FilesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SaveEditorIfDirty();
        SelectFile(FilesList.SelectedItem as FlowTextFile);
    }

    private void SelectFile(FlowTextFile? file)
    {
        _activeFile = file;
        SearchBox.Text = string.Empty;
        RefreshMessages();
        _activeMessage = _visibleMessages.FirstOrDefault();
        RefreshHeader();
        RefreshEditor();
    }

    private void RefreshHeader()
    {
        if (_activeChannel is null)
        {
            ChannelTitle.Text = "#nothing-yet";
            ChannelSubtitle.Text = L(
                "Create a workspace to start writing.",
                "Создай воркспейс, чтобы начать писать.",
                "Erstelle einen Arbeitsbereich, um mit dem Schreiben zu beginnen.");
            return;
        }

        ChannelTitle.Text = $"#{_activeChannel.Name}";
        ChannelSubtitle.Text = _activeFile is null
            ? L(
                $"{_activeChannel.Files.Count} files",
                $"{_activeChannel.Files.Count} файлов",
                $"{_activeChannel.Files.Count} Dateien")
            : L(
                $"{_activeFile.Name} - {_activeFile.Messages.Count} messages, {_activeFile.WordCount} words - {_activeChannel.LocationPath}",
                $"{_activeFile.Name} - сообщений: {_activeFile.Messages.Count}, слов: {_activeFile.WordCount} - {_activeChannel.LocationPath}",
                $"{_activeFile.Name} - Nachrichten: {_activeFile.Messages.Count}, Wörter: {_activeFile.WordCount} - {_activeChannel.LocationPath}");
    }

    private void RefreshMessages()
    {
        ClearHoveredStreamParagraph(force: true);
        _visibleMessages.Clear();
        _streamParagraphMessages.Clear();

        var document = CreateStreamDocument();

        if (_activeFile is null)
        {
            document.Blocks.Add(CreateHintParagraph(L(
                "Create or select a file inside this workspace.",
                "Создай или выбери файл внутри этого воркспейса.",
                "Erstelle oder wähle eine Datei in diesem Arbeitsbereich.")));
            MessageStreamBox.Document = document;
            SearchStatsText.Text = L("0 messages", "0 сообщений", "0 Nachrichten");
            return;
        }

        var query = SearchBox.Text.Trim();
        var messages = _activeFile.Messages.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            messages = messages.Where(message => message.Body.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        foreach (var message in messages.OrderBy(message => message.CreatedAt))
        {
            _visibleMessages.Add(message);
            var paragraph = CreateMessageParagraph(message);
            _streamParagraphMessages[paragraph] = message;
            document.Blocks.Add(paragraph);
        }

        if (_visibleMessages.Count == 0)
        {
            document.Blocks.Add(CreateHintParagraph(L("No messages here yet.", "Здесь пока нет сообщений.", "Hier gibt es noch keine Nachrichten.")));
        }

        MessageStreamBox.Document = document;
        SearchStatsText.Text = L(
            $"{_visibleMessages.Count} of {_activeFile.Messages.Count}",
            $"{_visibleMessages.Count} из {_activeFile.Messages.Count}",
            $"{_visibleMessages.Count} von {_activeFile.Messages.Count}");
    }

    private FlowDocument CreateStreamDocument()
    {
        return new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize = 15,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(_currentInkColor)
        };
    }

    private Paragraph CreateHintParagraph(string text)
    {
        return new Paragraph(new Run(text))
        {
            Foreground = new SolidColorBrush(_currentTheme.Muted),
            Margin = new Thickness(0)
        };
    }

    private Paragraph CreateMessageParagraph(FlowMessage message)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(6, 3, 6, 3),
            LineHeight = 22
        };

        foreach (var segment in BuildRenderSegments(message))
        {
            AppendRenderedSegment(paragraph, segment);
        }

        return paragraph;
    }

    private List<RenderSegment> BuildRenderSegments(FlowMessage message)
    {
        var marks = ExtractTextMarks(message);
        var chars = ParseMessageMarkup(message.Body, message.Id);
        var segments = new List<RenderSegment>();
        var builder = new System.Text.StringBuilder();
        TextRenderStyle? currentStyle = null;
        string? currentSpoilerKey = null;
        Color? currentMarkColor = null;

        foreach (var item in chars)
        {
            var markColor = FindMarkColor(marks, item.RawIndex);
            if (currentStyle == item.Style &&
                currentSpoilerKey == item.SpoilerKey &&
                Nullable.Equals(currentMarkColor, markColor))
            {
                builder.Append(item.Character);
                continue;
            }

            FlushSegment();
            currentStyle = item.Style;
            currentSpoilerKey = item.SpoilerKey;
            currentMarkColor = markColor;
            builder.Append(item.Character);
        }

        FlushSegment();
        return segments;

        void FlushSegment()
        {
            if (builder.Length == 0 || currentStyle is null)
            {
                return;
            }

            segments.Add(new RenderSegment(builder.ToString(), currentStyle.Value, currentSpoilerKey, currentMarkColor));
            builder.Clear();
        }
    }

    private static List<RenderChar> ParseMessageMarkup(string text, string messageId)
    {
        var chars = new List<RenderChar>();
        var spoilerIndex = 0;

        for (var i = 0; i < text.Length;)
        {
            if (TryReadDelimited(text, i, "\"\"", "\"\"", out var boldEnd))
            {
                for (var j = i + 2; j < boldEnd; j++)
                {
                    chars.Add(new RenderChar(text[j], j, TextRenderStyle.Bold, null));
                }

                i = boldEnd + 2;
                continue;
            }

            if (TryReadDelimited(text, i, "<<", ">>", out var italicEnd))
            {
                for (var j = i + 2; j < italicEnd; j++)
                {
                    chars.Add(new RenderChar(text[j], j, TextRenderStyle.Italic, null));
                }

                i = italicEnd + 2;
                continue;
            }

            if (TryReadDelimited(text, i, "__", "__", out var underlineEnd))
            {
                for (var j = i + 2; j < underlineEnd; j++)
                {
                    chars.Add(new RenderChar(text[j], j, TextRenderStyle.Underline, null));
                }

                i = underlineEnd + 2;
                continue;
            }

            if (TryReadDelimited(text, i, "~~", "~~", out var strikeEnd))
            {
                for (var j = i + 2; j < strikeEnd; j++)
                {
                    chars.Add(new RenderChar(text[j], j, TextRenderStyle.Strike, null));
                }

                i = strikeEnd + 2;
                continue;
            }

            if (TryReadDelimited(text, i, "`", "`", out var monoEnd))
            {
                for (var j = i + 1; j < monoEnd; j++)
                {
                    chars.Add(new RenderChar(text[j], j, TextRenderStyle.Monospace, null));
                }

                i = monoEnd + 1;
                continue;
            }

            if (StartsWith(text, i, "||"))
            {
                var end = text.IndexOf("||", i + 2, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    var spoilerKey = $"{messageId}:{spoilerIndex++}:{i}";
                    for (var j = i + 2; j < end; j++)
                    {
                        chars.Add(new RenderChar(text[j], j, TextRenderStyle.None, spoilerKey));
                    }

                    i = end + 2;
                    continue;
                }
            }

            chars.Add(new RenderChar(text[i], i, TextRenderStyle.None, null));
            i++;
        }

        return chars;
    }

    private static bool TryReadDelimited(string text, int index, string open, string close, out int end)
    {
        end = -1;
        if (!StartsWith(text, index, open))
        {
            return false;
        }

        end = text.IndexOf(close, index + open.Length, StringComparison.Ordinal);
        return end > index + open.Length;
    }

    private static bool StartsWith(string text, int index, string value)
    {
        return index + value.Length <= text.Length &&
               string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    private static List<TextMark> ExtractTextMarks(FlowMessage message)
    {
        var marks = new List<TextMark>();
        if (string.IsNullOrWhiteSpace(message.DocumentXaml))
        {
            return marks;
        }

        try
        {
            if (XamlReader.Parse(message.DocumentXaml) is not FlowDocument document)
            {
                return marks;
            }

            var offset = 0;
            var isFirstBlock = true;
            foreach (var paragraph in document.Blocks.OfType<Paragraph>())
            {
                if (!isFirstBlock)
                {
                    offset++;
                }

                ExtractInlineMarks(paragraph.Inlines, paragraph.Background, marks, ref offset);
                isFirstBlock = false;
            }
        }
        catch
        {
            return marks;
        }

        return marks;
    }

    private static void ExtractInlineMarks(InlineCollection inlines, Brush? inheritedBackground, List<TextMark> marks, ref int offset)
    {
        foreach (var inline in inlines)
        {
            var background = inline.Background ?? inheritedBackground;
            switch (inline)
            {
                case Run run:
                    AddMarkIfNeeded(marks, offset, run.Text.Length, background);
                    offset += run.Text.Length;
                    break;
                case LineBreak:
                    offset++;
                    break;
                case Span span:
                    ExtractInlineMarks(span.Inlines, background, marks, ref offset);
                    break;
            }
        }
    }

    private static void AddMarkIfNeeded(List<TextMark> marks, int start, int length, Brush? brush)
    {
        if (length <= 0 || !TryGetMarkColor(brush, out var color))
        {
            return;
        }

        marks.Add(new TextMark(start, start + length, color));
    }

    private static bool TryGetMarkColor(Brush? brush, out Color color)
    {
        color = Colors.Transparent;
        if (brush is not SolidColorBrush solid || solid.Opacity <= 0 || solid.Color.A == 0)
        {
            return false;
        }

        color = solid.Color;
        return true;
    }

    private static Color? FindMarkColor(List<TextMark> marks, int rawIndex)
    {
        for (var i = marks.Count - 1; i >= 0; i--)
        {
            var mark = marks[i];
            if (rawIndex >= mark.Start && rawIndex < mark.End)
            {
                return mark.Color;
            }
        }

        return null;
    }

    private void AppendRenderedSegment(Paragraph paragraph, RenderSegment segment)
    {
        if (segment.SpoilerKey is null)
        {
            AppendRuns(paragraph.Inlines, segment.Text, segment.Style, new SolidColorBrush(_currentInkColor), CreateBrush(segment.MarkColor));
            return;
        }

        var isOpen = _openSpoilers.Contains(segment.SpoilerKey);
        var span = new Span
        {
            Tag = segment.SpoilerKey,
            Cursor = Cursors.Hand,
            ToolTip = isOpen
                ? L("Click to hide spoiler", "Нажми, чтобы скрыть спойлер", "Klicken, um den Spoiler zu verbergen")
                : L("Click to reveal spoiler", "Нажми, чтобы открыть спойлер", "Klicken, um den Spoiler zu öffnen")
        };
        span.MouseLeftButtonUp += SpoilerSpan_MouseLeftButtonUp;

        var foreground = new SolidColorBrush(isOpen ? _currentInkColor : SpoilerHiddenColor);
        var background = isOpen
            ? CreateBrush(segment.MarkColor) ?? new SolidColorBrush(SpoilerOpenColor)
            : new SolidColorBrush(SpoilerHiddenColor);

        AppendRuns(span.Inlines, segment.Text, segment.Style, foreground, background);
        paragraph.Inlines.Add(span);
    }

    private static Brush? CreateBrush(Color? color)
    {
        return color is null ? null : new SolidColorBrush(color.Value);
    }

    private static void AppendRuns(InlineCollection inlines, string text, TextRenderStyle style, Brush foreground, Brush? background)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            if (lines[i].Length == 0)
            {
                continue;
            }

            var run = new Run(lines[i])
            {
                Foreground = foreground
            };

            if (background is not null)
            {
                run.Background = background;
            }

            if (style.HasFlag(TextRenderStyle.Bold))
            {
                run.FontWeight = FontWeights.SemiBold;
            }

            if (style.HasFlag(TextRenderStyle.Italic))
            {
                run.FontStyle = FontStyles.Italic;
            }

            if (style.HasFlag(TextRenderStyle.Underline))
            {
                run.TextDecorations = TextDecorations.Underline;
            }

            if (style.HasFlag(TextRenderStyle.Strike))
            {
                run.TextDecorations = TextDecorations.Strikethrough;
            }

            if (style.HasFlag(TextRenderStyle.Monospace))
            {
                run.FontFamily = new FontFamily("Consolas");
                run.Background ??= new SolidColorBrush(Color.FromRgb(241, 245, 249));
            }

            inlines.Add(run);
        }
    }

    private void MessageStreamBox_MouseMove(object sender, MouseEventArgs e)
    {
        var position = MessageStreamBox.GetPositionFromPoint(e.GetPosition(MessageStreamBox), true);
        var paragraph = position?.Paragraph;

        if (paragraph is not null && _streamParagraphMessages.TryGetValue(paragraph, out var message))
        {
            SetHoveredStreamParagraph(paragraph, message);
            return;
        }

        ClearHoveredStreamParagraph();
    }

    private void MessageStreamHost_MouseLeave(object sender, MouseEventArgs e)
    {
        ClearHoveredStreamParagraph();
    }

    private void SetHoveredStreamParagraph(Paragraph paragraph, FlowMessage message)
    {
        if (_hoveredStreamParagraph == paragraph)
        {
            return;
        }

        ClearHoveredStreamParagraph();
        _hoveredStreamParagraph = paragraph;
        _hoveredMessage = message;
        _hoveredStreamParagraph.Background = new SolidColorBrush(_currentTheme.Ghost);
        ShowMessageActionsButton(paragraph);
    }

    private void ShowMessageActionsButton(Paragraph paragraph)
    {
        var rect = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        var top = Math.Max(8, rect.Top - 3);
        MessageActionsButton.Margin = new Thickness(0, top, 14, 0);
        MessageActionsButton.Visibility = Visibility.Visible;
    }

    private void ClearHoveredStreamParagraph(bool force = false)
    {
        if (_messageActionsMenuOpen && !force)
        {
            return;
        }

        if (_hoveredStreamParagraph is null)
        {
            _hoveredMessage = null;
            MessageActionsButton.Visibility = Visibility.Collapsed;
            return;
        }

        _hoveredStreamParagraph.Background = null;
        _hoveredStreamParagraph = null;
        _hoveredMessage = null;
        MessageActionsButton.Visibility = Visibility.Collapsed;
    }

    private void MessageActionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hoveredMessage is null || MessageActionsButton.ContextMenu is null)
        {
            return;
        }

        _messageActionsTargetMessage = _hoveredMessage;
        _messageActionsMenuOpen = true;
        MessageActionsButton.ContextMenu.PlacementTarget = MessageActionsButton;
        MessageActionsButton.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void MessageActionsContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _messageActionsMenuOpen = true;
    }

    private void MessageActionsContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _messageActionsMenuOpen = false;
        _messageActionsTargetMessage = null;
        ClearHoveredStreamParagraph(force: true);
    }

    private void EditHoveredMessageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_messageActionsTargetMessage is not null)
        {
            SelectMessageForEditing(_messageActionsTargetMessage);
        }
    }

    private void DuplicateHoveredMessageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_messageActionsTargetMessage is not null)
        {
            DuplicateMessage(_messageActionsTargetMessage);
        }
    }

    private void CopyHoveredMessageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_messageActionsTargetMessage is not null)
        {
            CopyMessageText(_messageActionsTargetMessage);
        }
    }

    private void DeleteHoveredMessageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_messageActionsTargetMessage is not null)
        {
            DeleteMessage(_messageActionsTargetMessage);
        }
    }

    private void MessageStreamBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = MessageStreamBox.GetPositionFromPoint(e.GetPosition(MessageStreamBox), true);
        var paragraph = position?.Paragraph;

        if (paragraph is not null && _streamParagraphMessages.TryGetValue(paragraph, out var message))
        {
            _messageContextTargetMessage = message;
            SetHoveredStreamParagraph(paragraph, message);
            return;
        }

        _messageContextTargetMessage = null;
        e.Handled = true;
    }

    private void MessageStreamBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_messageContextTargetMessage is null)
        {
            e.Handled = true;
        }
    }

    private void EditMessageContextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_messageContextTargetMessage is not null)
        {
            SelectMessageForEditing(_messageContextTargetMessage);
        }
    }

    private void DuplicateMessageContextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_messageContextTargetMessage is not null)
        {
            DuplicateMessage(_messageContextTargetMessage);
        }
    }

    private void CopyMessageContextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_messageContextTargetMessage is not null)
        {
            CopyMessageText(_messageContextTargetMessage);
        }
    }

    private void DeleteMessageContextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_messageContextTargetMessage is not null)
        {
            DeleteMessage(_messageContextTargetMessage);
        }
    }

    private void MessageStreamBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrEmpty(MessageStreamBox.Selection.Text))
        {
            return;
        }

        var position = MessageStreamBox.GetPositionFromPoint(e.GetPosition(MessageStreamBox), true);
        if (TryGetSpoilerKey(position, out var spoilerKey))
        {
            ToggleSpoiler(spoilerKey);
            e.Handled = true;
            return;
        }

        var paragraph = position?.Paragraph;

        if (paragraph is not null && _streamParagraphMessages.TryGetValue(paragraph, out var message))
        {
            SelectMessageForEditing(message);
        }
    }

    private void SpoilerSpan_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Span { Tag: string spoilerKey })
        {
            return;
        }

        ToggleSpoiler(spoilerKey);
        e.Handled = true;
    }

    private void ToggleSpoiler(string spoilerKey)
    {
        if (!_openSpoilers.Add(spoilerKey))
        {
            _openSpoilers.Remove(spoilerKey);
        }

        RefreshMessages();
    }

    private static bool TryGetSpoilerKey(TextPointer? position, out string spoilerKey)
    {
        spoilerKey = string.Empty;
        var current = position?.Parent as DependencyObject;
        while (current is not null)
        {
            if (current is Span { Tag: string key })
            {
                spoilerKey = key;
                return true;
            }

            current = LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private void SelectMessageForEditing(FlowMessage message)
    {
        SaveEditorIfDirty();
        _activeMessage = message;
        RefreshEditor();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendComposerText();
    }

    private void ComposerBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SendComposerText();
            e.Handled = true;
        }
    }

    private void SendComposerText()
    {
        if (_activeFile is null)
        {
            return;
        }

        var text = ComposerBox.Text.Trim().ReplaceLineEndings("\n");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var message = new FlowMessage
        {
            Body = text,
            DocumentXaml = CreateDocumentXaml(text)
        };

        _activeFile.Messages.Add(message);
        ComposerBox.Clear();
        SelectMessageForEditing(message);
        MessageStreamBox.ScrollToEnd();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveEditorIfDirty();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedMessage();
    }

    private void DeleteSelectedMessage()
    {
        if (_activeMessage is null)
        {
            return;
        }

        DeleteMessage(_activeMessage);
    }

    private void DeleteMessage(FlowMessage message)
    {
        if (_activeFile is null)
        {
            return;
        }

        var messageTitle = GetDeletePreview(message.Body);
        SaveEditorIfDirty();
        var file = _activeFile;
        var index = _visibleMessages.IndexOf(message);
        if (index < 0)
        {
            index = file.Messages.IndexOf(message);
        }

        file.Messages.Remove(message);
        RefreshMessages();

        if (_activeMessage == message)
        {
            _activeMessage = _visibleMessages.Count == 0
                ? null
                : _visibleMessages[Math.Clamp(index, 0, _visibleMessages.Count - 1)];
            RefreshEditor();
        }

        ClearHoveredStreamParagraph(force: true);
        ShowUndoDeleteItem(
            L(
                $"Message deleted: \"{messageTitle}\"",
                $"Сообщение удалено: \"{messageTitle}\"",
                $"Nachricht gelöscht: \"{messageTitle}\""),
            () =>
            {
                var ownerChannel = FindChannelForFile(file);
                if (ownerChannel is null)
                {
                    return;
                }

                if (!file.Messages.Contains(message))
                {
                    var insertIndex = Math.Clamp(index, 0, file.Messages.Count);
                    file.Messages.Insert(insertIndex, message);
                }

                ChannelsList.SelectedItem = ownerChannel;
                FilesList.SelectedItem = file;
                SelectFile(file);
                SelectMessageForEditing(message);
                SaveLibrary();
            });
    }

    private void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMessage is null)
        {
            return;
        }

        DuplicateMessage(_activeMessage);
    }

    private void DuplicateMessage(FlowMessage message)
    {
        if (_activeFile is null)
        {
            return;
        }

        var duplicate = message.CloneAsDraft();
        var index = _activeFile.Messages.IndexOf(message);
        _activeFile.Messages.Insert(Math.Clamp(index + 1, 0, _activeFile.Messages.Count), duplicate);
        SelectMessageForEditing(duplicate);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMessage is not null)
        {
            Clipboard.SetText(GetEditorText());
            EditorStateText.Text = L("Copied", "Скопировано", "Kopiert");
        }
    }

    private void CopyMessageText(FlowMessage message)
    {
        Clipboard.SetText(message.Body);
        EditorStateText.Text = L("Copied", "Скопировано", "Kopiert");
    }

    private void MarkButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorBackground(_selectedMarkColor, L("Marked", "Отмечено", "Markiert"));
    }

    private void ClearMarkButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorBackground(null, L("Mark cleared", "Маркер убран", "Marker entfernt"));
    }

    private void MarkColorSwatch_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        MarkColorPopup.IsOpen = true;
    }

    private void MarkColorSwatch_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void MarkColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string colorText })
        {
            return;
        }

        if (ColorConverter.ConvertFromString(colorText) is not Color color)
        {
            return;
        }

        _selectedMarkColor = color;
        MarkColorSwatch.Background = new SolidColorBrush(color);
        MarkColorPopup.IsOpen = false;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveEditorIfDirty();
        var draft = _settings.Clone();
        if (SettingsDialog.Edit(this, draft, ThemeOptions, _storage.DisplayPath, _activeChannel?.LocationPath) is not { } updated)
        {
            return;
        }

        _settings = updated;
        ApplyAppSettings();
        SaveLibrary();
    }

    private void ApplyEditorBackground(Color? color, string stateText)
    {
        if (!EditorBox.IsEnabled)
        {
            return;
        }

        if (EditorBox.Selection.IsEmpty)
        {
            EditorStateText.Text = L("Select text first", "Сначала выдели текст", "Zuerst Text auswählen");
            return;
        }

        EditorBox.Focus();
        var brush = color is null ? Brushes.Transparent : new SolidColorBrush(color.Value);
        EditorBox.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
        _hasUnsavedEditorText = true;
        SaveEditorIfDirty();
        EditorStateText.Text = stateText;
    }

    private void WorkspaceItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
            item.ContextMenu = BuildWorkspaceContextMenu();
        }
    }

    private void RenameWorkspaceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextWorkspace(sender) is { } workspace)
        {
            RenameWorkspace(workspace);
        }
    }

    private void DuplicateWorkspaceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextWorkspace(sender) is { } workspace)
        {
            DuplicateWorkspace(workspace);
        }
    }

    private void DeleteWorkspaceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextWorkspace(sender) is { } workspace)
        {
            DeleteWorkspace(workspace);
        }
    }

    private void FileItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
            item.ContextMenu = BuildFileContextMenu();
        }
    }

    private ContextMenu BuildWorkspaceContextMenu()
    {
        var menu = CreateContextMenu();
        menu.Items.Add(CreateMenuItem(L("Rename", "Переименовать", "Umbenennen"), RenameWorkspaceMenuItem_Click));
        menu.Items.Add(CreateMenuItem(L("Duplicate", "Дублировать", "Duplizieren"), DuplicateWorkspaceMenuItem_Click));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem(L("Delete", "Удалить", "Löschen"), DeleteWorkspaceMenuItem_Click));
        return menu;
    }

    private ContextMenu BuildFileContextMenu()
    {
        var menu = CreateContextMenu();
        menu.Items.Add(CreateMenuItem(L("Rename", "Переименовать", "Umbenennen"), RenameFileMenuItem_Click));
        menu.Items.Add(CreateMenuItem(L("Duplicate", "Дублировать", "Duplizieren"), DuplicateFileMenuItem_Click));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem(L("Delete", "Удалить", "Löschen"), DeleteFileMenuItem_Click));
        return menu;
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        ApplyContextMenuStyle(menu);
        return menu;
    }

    private MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        ApplyContextMenuItemStyle(item);
        item.Click += handler;
        return item;
    }

    private Separator CreateSeparator()
    {
        var separator = new Separator();
        ApplyContextMenuItemStyle(separator);
        return separator;
    }

    private void ApplyContextMenuStyle(ContextMenu? menu)
    {
        if (menu is null)
        {
            return;
        }

        if (TryFindResource(typeof(ContextMenu)) is Style menuStyle)
        {
            menu.Style = menuStyle;
        }

        foreach (var item in menu.Items)
        {
            ApplyContextMenuItemStyle(item);
        }
    }

    private void ApplyContextMenuItemStyle(object? item)
    {
        switch (item)
        {
            case MenuItem menuItem:
                if (TryFindResource(typeof(MenuItem)) is Style menuItemStyle)
                {
                    menuItem.Style = menuItemStyle;
                }

                foreach (var child in menuItem.Items)
                {
                    ApplyContextMenuItemStyle(child);
                }

                break;

            case Separator separator:
                if (TryFindResource(typeof(Separator)) is Style separatorStyle)
                {
                    separator.Style = separatorStyle;
                }

                break;
        }
    }

    private void RenameFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextFile(sender) is { } file)
        {
            RenameFile(file);
        }
    }

    private void DuplicateFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextFile(sender) is { } file)
        {
            DuplicateFile(file);
        }
    }

    private void DeleteFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextFile(sender) is { } file)
        {
            DeleteFile(file);
        }
    }

    private FlowChannel? GetContextWorkspace(object sender)
    {
        return GetContextData<FlowChannel>(sender);
    }

    private FlowTextFile? GetContextFile(object sender)
    {
        return GetContextData<FlowTextFile>(sender);
    }

    private static T? GetContextData<T>(object sender) where T : class
    {
        return sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement target } }
            ? target.DataContext as T
            : null;
    }

    private void AddChannelButton_Click(object sender, RoutedEventArgs e)
    {
        AddChannelFromInput();
    }

    private void NewChannelBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddChannelFromInput();
            e.Handled = true;
        }
    }

    private void AddChannelFromInput()
    {
        var requestedName = NormalizeWorkspaceName(NewChannelBox.Text);
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return;
        }

        var location = ChooseWorkspaceLocation(requestedName, out var actualName);
        if (location is null)
        {
            return;
        }

        var channel = new FlowChannel { Name = actualName, LocationPath = location };
        channel.Files.Add(new FlowTextFile { Name = "main" });
        AttachChannel(channel);
        _channels.Add(channel);
        NewChannelBox.Clear();
        ChannelsList.SelectedItem = channel;
        SaveLibrary();
    }

    private string? ChooseWorkspaceLocation(string workspaceName, out string actualName)
    {
        actualName = workspaceName;
        var dialog = new OpenFolderDialog
        {
            Title = L(
                $"Choose where to create workspace folder \"{workspaceName}\"",
                $"Выбери, где создать папку воркспейса \"{workspaceName}\"",
                $"Wähle, wo der Ordner für \"{workspaceName}\" erstellt werden soll"),
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return null;
        }

        return CreateWorkspaceFolder(dialog.FolderName, workspaceName, _channels.Select(channel => channel.Name), out actualName);
    }

    private static string NormalizeWorkspaceName(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Replace(' ', '-');
        return string.Concat(normalized.Where(character => char.IsLetterOrDigit(character) || character == '-'));
    }

    private void AddFileButton_Click(object sender, RoutedEventArgs e)
    {
        AddFileFromInput();
    }

    private void NewFileBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddFileFromInput();
            e.Handled = true;
        }
    }

    private void AddFileFromInput()
    {
        if (_activeChannel is null)
        {
            return;
        }

        var name = NormalizeFileName(NewFileBox.Text);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var file = new FlowTextFile { Name = name };
        _activeChannel.Files.Add(file);
        NewFileBox.Clear();
        FilesList.SelectedItem = file;
        SelectFile(file);
        SaveLibrary();
    }

    private static string NormalizeFileName(string value)
    {
        var normalized = value.Trim();
        return string.Concat(normalized.Where(character =>
            char.IsLetterOrDigit(character) ||
            character is ' ' or '-' or '_')).Trim();
    }

    private void RenameWorkspace(FlowChannel workspace)
    {
        var name = TextPromptDialog.Prompt(
            this,
            L("Rename workspace", "Переименовать воркспейс", "Arbeitsbereich umbenennen"),
            L("Workspace name", "Название воркспейса", "Name des Arbeitsbereichs"),
            workspace.Name,
            _currentTheme,
            _settings.Language);
        if (name is null)
        {
            return;
        }

        var normalized = NormalizeWorkspaceName(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var location = MoveWorkspaceFolderForRename(workspace, normalized, out var actualName, out var moveError);
        workspace.Name = actualName;
        if (location is not null)
        {
            workspace.LocationPath = location;
        }

        RefreshHeader();
        SaveLibrary();
        if (moveError is not null)
        {
            SaveStatusText.Text = L(
                $"Workspace renamed, folder stayed put: {moveError}",
                $"Воркспейс переименован, папка осталась на месте: {moveError}",
                $"Arbeitsbereich umbenannt, Ordner blieb unverändert: {moveError}");
        }
    }

    private string? MoveWorkspaceFolderForRename(
        FlowChannel workspace,
        string requestedName,
        out string actualName,
        out string? moveError)
    {
        actualName = requestedName;
        moveError = null;

        if (string.IsNullOrWhiteSpace(workspace.LocationPath))
        {
            return null;
        }

        var currentPath = workspace.LocationPath;
        var parentFolder = Directory.GetParent(currentPath)?.FullName;
        if (string.IsNullOrWhiteSpace(parentFolder))
        {
            return currentPath;
        }

        try
        {
            Directory.CreateDirectory(parentFolder);
            var reservedNames = _channels
                .Where(channel => !ReferenceEquals(channel, workspace))
                .Select(channel => channel.Name);
            var nextPath = MakeAvailableWorkspaceFolderPath(parentFolder, requestedName, reservedNames, currentPath, out actualName);
            if (IsSamePath(currentPath, nextPath))
            {
                return currentPath;
            }

            if (Directory.Exists(currentPath))
            {
                Directory.Move(currentPath, nextPath);
            }
            else
            {
                Directory.CreateDirectory(nextPath);
            }

            return nextPath;
        }
        catch (Exception ex)
        {
            moveError = ex.Message;
            return currentPath;
        }
    }

    private void DuplicateWorkspace(FlowChannel workspace)
    {
        var duplicateName = MakeUniqueWorkspaceName($"{workspace.Name}-copy");
        var location = ChooseWorkspaceLocation(duplicateName, out var actualName);
        if (location is null)
        {
            return;
        }

        var duplicate = CloneWorkspace(workspace, actualName, location);
        AttachChannel(duplicate);

        var index = _channels.IndexOf(workspace);
        _channels.Insert(Math.Clamp(index + 1, 0, _channels.Count), duplicate);
        ChannelsList.SelectedItem = duplicate;
        SaveLibrary();
    }

    private static FlowChannel CloneWorkspace(FlowChannel workspace, string name, string location)
    {
        var clone = new FlowChannel
        {
            Name = name,
            LocationPath = location
        };

        foreach (var file in workspace.Files)
        {
            clone.Files.Add(CloneFile(file, file.Name));
        }

        return clone;
    }

    private string MakeUniqueWorkspaceName(string baseName)
    {
        return MakeUniqueName(baseName, _channels.Select(channel => channel.Name));
    }

    private void RenameFile(FlowTextFile file)
    {
        var name = TextPromptDialog.Prompt(
            this,
            L("Rename file", "Переименовать файл", "Datei umbenennen"),
            L("File name", "Название файла", "Dateiname"),
            file.Name,
            _currentTheme,
            _settings.Language);
        if (name is null)
        {
            return;
        }

        var normalized = NormalizeFileName(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        file.Name = normalized;
        RefreshHeader();
        SaveLibrary();
    }

    private void DuplicateFile(FlowTextFile file)
    {
        var channel = FindChannelForFile(file);
        if (channel is null)
        {
            return;
        }

        var duplicate = CloneFile(file, MakeUniqueFileName(channel, $"{file.Name} copy"));
        var index = channel.Files.IndexOf(file);
        channel.Files.Insert(Math.Clamp(index + 1, 0, channel.Files.Count), duplicate);
        FilesList.SelectedItem = duplicate;
        SelectFile(duplicate);
        SaveLibrary();
    }

    private static FlowTextFile CloneFile(FlowTextFile file, string name)
    {
        var clone = new FlowTextFile
        {
            Name = name
        };

        foreach (var message in file.Messages)
        {
            clone.Messages.Add(message.CloneAsDraft());
        }

        return clone;
    }

    private static string MakeUniqueFileName(FlowChannel channel, string baseName)
    {
        return MakeUniqueName(baseName, channel.Files.Select(file => file.Name));
    }

    private static string MakeUniqueName(string baseName, IEnumerable<string> existingNames)
    {
        var existing = existingNames.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName}-{i}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private void DeleteFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeFile is null)
        {
            return;
        }

        DeleteFile(_activeFile);
    }

    private void DeleteFile(FlowTextFile fileToDelete)
    {
        var channel = FindChannelForFile(fileToDelete);
        if (channel is null)
        {
            return;
        }

        if (channel.Files.Count <= 1)
        {
            SaveStatusText.Text = L("Keep at least one file", "Оставь хотя бы один файл", "Mindestens eine Datei behalten");
            return;
        }

        SaveEditorIfDirty();
        if (!ConfirmDelete(
                L("file", "файл", "Datei"),
                fileToDelete.Name,
                L("This will remove the file and every message inside it.", "Это удалит файл и все сообщения внутри него.", "Dies entfernt die Datei und alle Nachrichten darin.")))
        {
            return;
        }

        var index = channel.Files.IndexOf(fileToDelete);
        var wasActive = fileToDelete == _activeFile;
        channel.Files.Remove(fileToDelete);
        if (wasActive)
        {
            var nextFile = channel.Files[Math.Clamp(index, 0, channel.Files.Count - 1)];
            FilesList.SelectedItem = nextFile;
            SelectFile(nextFile);
        }

        ShowUndoDeleteItem(
            L(
                $"File \"{fileToDelete.Name}\" deleted.",
                $"Файл \"{fileToDelete.Name}\" удалён.",
                $"Datei \"{fileToDelete.Name}\" gelöscht."),
            () =>
            {
                if (!_channels.Contains(channel))
                {
                    return;
                }

                if (!channel.Files.Contains(fileToDelete))
                {
                    var insertIndex = Math.Clamp(index, 0, channel.Files.Count);
                    channel.Files.Insert(insertIndex, fileToDelete);
                }

                ChannelsList.SelectedItem = channel;
                FilesList.SelectedItem = fileToDelete;
                SelectFile(fileToDelete);
                SaveLibrary();
            });
        SaveLibrary();
    }

    private void DeleteChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeChannel is null)
        {
            return;
        }

        DeleteWorkspace(_activeChannel);
    }

    private void DeleteWorkspace(FlowChannel channelToDelete)
    {
        if (_channels.Count <= 1)
        {
            SaveStatusText.Text = L("Keep at least one workspace", "Оставь хотя бы один воркспейс", "Mindestens einen Arbeitsbereich behalten");
            return;
        }

        SaveEditorIfDirty();

        if (!ConfirmDelete(
                L("workspace", "воркспейс", "Arbeitsbereich"),
                channelToDelete.Name,
                L("This will remove the workspace, its files, and its messages.", "Это удалит воркспейс, его файлы и сообщения.", "Dies entfernt den Arbeitsbereich, seine Dateien und Nachrichten.")))
        {
            return;
        }

        var index = _channels.IndexOf(channelToDelete);
        var wasActive = channelToDelete == _activeChannel;
        _channels.Remove(channelToDelete);
        if (wasActive)
        {
            ChannelsList.SelectedIndex = Math.Min(index, _channels.Count - 1);
        }

        ShowUndoDeleteItem(
            L(
                $"Workspace \"{channelToDelete.Name}\" deleted.",
                $"Воркспейс \"{channelToDelete.Name}\" удалён.",
                $"Arbeitsbereich \"{channelToDelete.Name}\" gelöscht."),
            () =>
            {
                if (!_channels.Contains(channelToDelete))
                {
                    var insertIndex = Math.Clamp(index, 0, _channels.Count);
                    _channels.Insert(insertIndex, channelToDelete);
                }

                ChannelsList.SelectedItem = channelToDelete;
                SaveLibrary();
            });
        SaveLibrary();
    }

    private bool ConfirmDelete(string itemType, string itemName, string detail)
    {
        return DeleteConfirmationDialog.Confirm(this, itemType, itemName, detail, _currentTheme, _settings.Language);
    }

    private FlowChannel? FindChannelForFile(FlowTextFile file)
    {
        return _channels.FirstOrDefault(channel => channel.Files.Contains(file));
    }

    private static string GetDeletePreview(string text)
    {
        var preview = text.ReplaceLineEndings(" ").Trim();
        return preview.Length <= 54 ? preview : string.Concat(preview.AsSpan(0, 54), "...");
    }

    private void ShowUndoDeleteItem(string text, Action restoreAction)
    {
        var card = BuildUndoDeleteCard(text, restoreAction);
        UndoDeleteList.Children.Insert(0, card);
    }

    private Border BuildUndoDeleteCard(string text, Action restoreAction)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        var card = new Border
        {
            Background = new SolidColorBrush(_currentTheme.Side),
            BorderBrush = new SolidColorBrush(_currentTheme.WorkspaceHover),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        void RemoveCard()
        {
            timer.Stop();
            UndoDeleteList.Children.Remove(card);
        }

        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(_currentTheme.SideText),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 9, 0, 0)
        };

        var undoButton = CreateUndoToastButton(L("Undo", "Вернуть", "Rückgängig"), new SolidColorBrush(_currentTheme.Accent), Brushes.White);
        undoButton.Click += (_, _) =>
        {
            restoreAction();
            RemoveCard();
        };

        var hideButton = CreateUndoToastButton(L("Hide", "Скрыть", "Ausblenden"), new SolidColorBrush(_currentTheme.Ghost), new SolidColorBrush(_currentTheme.Ink));
        hideButton.Margin = new Thickness(8, 0, 0, 0);
        hideButton.Click += (_, _) => RemoveCard();

        actions.Children.Add(undoButton);
        actions.Children.Add(hideButton);

        card.Child = new StackPanel
        {
            Children =
            {
                textBlock,
                actions
            }
        };

        timer.Tick += (_, _) => RemoveCard();
        timer.Start();
        return card;
    }

    private static Button CreateUndoToastButton(string text, Brush background, Brush foreground)
    {
        return new Button
        {
            Content = text,
            Width = 72,
            MinHeight = 30,
            Padding = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(0),
            Background = background,
            Foreground = foreground,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshMessages();
    }

    private void EditorBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isLoadingEditor)
        {
            return;
        }

        _hasUnsavedEditorText = true;
        EditorStateText.Text = L("Unsaved", "Не сохранено", "Nicht gespeichert");
    }

    private void SaveEditorIfDirty()
    {
        if (!_hasUnsavedEditorText || _activeMessage is null)
        {
            return;
        }

        _activeMessage.DocumentXaml = SerializeEditorDocument();
        _activeMessage.Body = GetEditorText();
        _hasUnsavedEditorText = false;
        EditorStateText.Text = L("Saved", "Сохранено", "Gespeichert");
        SaveLibrary();
    }

    private void RefreshEditor()
    {
        _isLoadingEditor = true;
        _hasUnsavedEditorText = false;

        if (_activeMessage is null)
        {
            EditorHint.Text = L(
                "Click a message in the stream to edit that message here.",
                "Нажми на сообщение в потоке, чтобы редактировать его здесь.",
                "Klicke auf eine Nachricht im Stream, um sie hier zu bearbeiten.");
            EditorStateText.Text = string.Empty;
            EditorBox.Document = CreateDocument(string.Empty);
            EditorBox.IsEnabled = false;
            _isLoadingEditor = false;
            return;
        }

        EditorHint.Text = $"{_activeMessage.TimestampText} - {_activeMessage.WordCountText}";
        EditorStateText.Text = L("Saved", "Сохранено", "Gespeichert");
        EditorBox.IsEnabled = true;
        EditorBox.Document = CreateDocument(_activeMessage);
        _isLoadingEditor = false;
    }

    private string GetEditorText()
    {
        return new TextRange(EditorBox.Document.ContentStart, EditorBox.Document.ContentEnd).Text
            .Trim()
            .ReplaceLineEndings("\n");
    }

    private string SerializeEditorDocument()
    {
        return XamlWriter.Save(EditorBox.Document);
    }

    private static FlowDocument CreateDocument(FlowMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.DocumentXaml))
        {
            try
            {
                if (XamlReader.Parse(message.DocumentXaml) is FlowDocument document)
                {
                    document.PagePadding = new Thickness(0);
                    return document;
                }
            }
            catch
            {
                // Fall back to plain text if stored rich text cannot be parsed.
            }
        }

        return CreateDocument(message.Body);
    }

    private static FlowDocument CreateDocument(string text)
    {
        return new FlowDocument(new Paragraph(new Run(text)))
        {
            PagePadding = new Thickness(0)
        };
    }

    private static string CreateDocumentXaml(string text)
    {
        return XamlWriter.Save(CreateDocument(text));
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveEditorIfDirty();
        SaveLibrary();
    }

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

public sealed class SettingsDialog : Window
{
    private readonly FlowAppSettings _settings;
    private readonly IReadOnlyList<AppThemeOption> _themes;
    private readonly AppThemeOption _theme;
    private readonly RadioButton _englishButton = new();
    private readonly RadioButton _russianButton = new();
    private readonly RadioButton _germanButton = new();
    private readonly ComboBox _markerBox = new();

    private SettingsDialog(
        FlowAppSettings settings,
        IReadOnlyList<AppThemeOption> themes,
        string storagePath,
        string? activeWorkspacePath)
    {
        _settings = settings;
        _settings.Normalize();
        _themes = themes;
        _theme = themes.FirstOrDefault(theme => theme.Id == settings.Theme) ?? themes[0];

        Title = T("Settings", "Настройки", "Einstellungen");
        Width = 640;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        AllowsTransparency = true;
        FontFamily = new FontFamily("Segoe UI");
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };

        Content = new Border
        {
            Background = new SolidColorBrush(_theme.Panel),
            BorderBrush = new SolidColorBrush(_theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent(storagePath, activeWorkspacePath)
        };
    }

    public static FlowAppSettings? Edit(
        Window owner,
        FlowAppSettings settings,
        IReadOnlyList<AppThemeOption> themes,
        string storagePath,
        string? activeWorkspacePath)
    {
        var dialog = new SettingsDialog(settings, themes, storagePath, activeWorkspacePath)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog._settings : null;
    }

    private UIElement BuildContent(string storagePath, string? activeWorkspacePath)
    {
        var root = new DockPanel();
        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var actions = BuildActions();
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);

        root.Children.Add(CreateSettingsTabs(storagePath, activeWorkspacePath));

        return root;
    }

    private TabControl CreateSettingsTabs(string storagePath, string? activeWorkspacePath)
    {
        var tabs = new TabControl
        {
            Margin = new Thickness(16),
            Background = new SolidColorBrush(_theme.Panel),
            BorderBrush = new SolidColorBrush(_theme.Line),
            Foreground = new SolidColorBrush(_theme.Ink),
            ItemContainerStyle = CreateSettingsTabItemStyle(),
            Template = CreateSettingsTabControlTemplate()
        };

        tabs.Items.Add(new TabItem { Header = T("Language", "Язык", "Sprache"), Content = BuildLanguageTab() });
        tabs.Items.Add(new TabItem { Header = T("Themes", "Темы", "Themen"), Content = BuildThemesTab() });
        tabs.Items.Add(new TabItem { Header = T("Editor", "Редактор", "Editor"), Content = BuildEditorTab() });
        tabs.Items.Add(new TabItem { Header = T("Storage", "Хранилище", "Speicher"), Content = BuildStorageTab(storagePath, activeWorkspacePath) });
        return tabs;
    }

    private ControlTemplate CreateSettingsTabControlTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(_theme.Panel));
        border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(_theme.Line));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

        var dock = new FrameworkElementFactory(typeof(DockPanel));
        border.AppendChild(dock);

        var headerShell = new FrameworkElementFactory(typeof(Border));
        headerShell.SetValue(DockPanel.DockProperty, Dock.Top);
        headerShell.SetValue(Border.BackgroundProperty, new SolidColorBrush(_theme.Ghost));
        headerShell.SetValue(Border.BorderBrushProperty, new SolidColorBrush(_theme.Line));
        headerShell.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
        headerShell.SetValue(Border.CornerRadiusProperty, new CornerRadius(8, 8, 0, 0));
        headerShell.SetValue(Border.PaddingProperty, new Thickness(8, 8, 8, 0));

        var tabPanel = new FrameworkElementFactory(typeof(TabPanel));
        tabPanel.Name = "HeaderPanel";
        tabPanel.SetValue(Panel.IsItemsHostProperty, true);
        headerShell.AppendChild(tabPanel);
        dock.AppendChild(headerShell);

        var contentShell = new FrameworkElementFactory(typeof(Border));
        contentShell.SetValue(Border.BackgroundProperty, new SolidColorBrush(_theme.Panel));
        contentShell.SetValue(Border.CornerRadiusProperty, new CornerRadius(0, 0, 8, 8));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.Name = "PART_SelectedContentHost";
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
        contentShell.AppendChild(presenter);
        dock.AppendChild(contentShell);

        return new ControlTemplate(typeof(TabControl))
        {
            VisualTree = border
        };
    }

    private Style CreateSettingsTabItemStyle()
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(_theme.Muted)));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 8, 14, 8)));
        style.Setters.Add(new Setter(Control.TemplateProperty, CreateSettingsTabItemTemplate()));
        return style;
    }

    private ControlTemplate CreateSettingsTabItemTemplate()
    {
        var shell = new FrameworkElementFactory(typeof(Border));
        shell.Name = "Shell";
        shell.SetValue(Border.BackgroundProperty, new SolidColorBrush(_theme.Ghost));
        shell.SetValue(Border.BorderBrushProperty, new SolidColorBrush(_theme.Line));
        shell.SetValue(Border.BorderThicknessProperty, new Thickness(1, 1, 1, 0));
        shell.SetValue(Border.CornerRadiusProperty, new CornerRadius(7, 7, 0, 0));
        shell.SetValue(Border.MarginProperty, new Thickness(0, 0, 6, 0));
        shell.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        shell.AppendChild(presenter);

        var template = new ControlTemplate(typeof(TabItem))
        {
            VisualTree = shell
        };

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(_theme.AccentSoft), "Shell"));
        hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(_theme.Ink)));
        template.Triggers.Add(hoverTrigger);

        var selectedTrigger = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(_theme.Panel), "Shell"));
        selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(_theme.Accent), "Shell"));
        selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(_theme.Ink)));
        template.Triggers.Add(selectedTrigger);

        return template;
    }

    private UIElement BuildHeader()
    {
        var grid = new Grid
        {
            Background = new SolidColorBrush(_theme.Side)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel
        {
            Margin = new Thickness(18, 15, 18, 14),
            Children =
            {
                new TextBlock
                {
                    Text = T("Settings", "Настройки", "Einstellungen"),
                    Foreground = new SolidColorBrush(_theme.SideText),
                    FontSize = 22,
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = T(
                        "Tune Thought Flow to feel closer to your writing space.",
                        "Настрой Thought Flow под своё рабочее пространство.",
                        "Passe Thought Flow an deinen Schreibraum an."),
                    Foreground = new SolidColorBrush(_theme.SideMuted),
                    Margin = new Thickness(0, 4, 0, 0)
                }
            }
        };
        grid.Children.Add(text);

        var close = CreateCloseButton(new SolidColorBrush(_theme.SideMuted), (_, _) => DialogResult = false);
        close.Width = 40;
        close.MinWidth = 40;
        close.Margin = new Thickness(0, 10, 12, 0);
        close.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(close, 1);
        grid.Children.Add(close);
        return new Border
        {
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Child = grid
        };
    }

    private static Button CreateCloseButton(Brush foreground, RoutedEventHandler onClick)
    {
        var firstStroke = new System.Windows.Shapes.Line
        {
            X1 = 3,
            Y1 = 3,
            X2 = 13,
            Y2 = 13,
            Stroke = foreground,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var secondStroke = new System.Windows.Shapes.Line
        {
            X1 = 13,
            Y1 = 3,
            X2 = 3,
            Y2 = 13,
            Stroke = foreground,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var icon = new Canvas
        {
            Width = 16,
            Height = 16,
            Children =
            {
                firstStroke,
                secondStroke
            }
        };

        var button = new Button
        {
            Content = icon,
            MinHeight = 36,
            MinWidth = 36,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = foreground,
            Cursor = Cursors.Hand
        };
        button.Click += onClick;
        return button;
    }

    private UIElement BuildLanguageTab()
    {
        _englishButton.Content = "English";
        _englishButton.Foreground = new SolidColorBrush(_theme.Ink);
        _englishButton.Margin = new Thickness(0, 0, 0, 10);
        _englishButton.IsChecked = _settings.Language == "en";
        _englishButton.Checked += (_, _) => _settings.Language = "en";

        _russianButton.Content = "Русский";
        _russianButton.Foreground = new SolidColorBrush(_theme.Ink);
        _russianButton.IsChecked = _settings.Language == "ru";
        _russianButton.Checked += (_, _) => _settings.Language = "ru";

        _germanButton.Content = "Deutsch";
        _germanButton.Foreground = new SolidColorBrush(_theme.Ink);
        _germanButton.Margin = new Thickness(0, 10, 0, 0);
        _germanButton.IsChecked = _settings.Language == "de";
        _germanButton.Checked += (_, _) => _settings.Language = "de";

        return BuildTabBody(
            T("Language", "Язык", "Sprache"),
            T(
                "Choose the interface language. The app can switch immediately after saving.",
                "Выбери язык интерфейса. После сохранения приложение сразу переключится.",
                "Wähle die Sprache der Oberfläche. Nach dem Speichern wechselt die App sofort."),
            new StackPanel
            {
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    _englishButton,
                    _russianButton,
                    _germanButton
                }
            });
    }

    private UIElement BuildThemesTab()
    {
        var list = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        foreach (var option in _themes)
        {
            var button = new RadioButton
            {
                Tag = option.Id,
                IsChecked = option.Id == _settings.Theme,
                Foreground = new SolidColorBrush(_theme.Ink),
                Margin = new Thickness(0, 0, 0, 12),
                Content = BuildThemeRow(option)
            };
            button.Checked += (_, _) => _settings.Theme = option.Id;
            list.Children.Add(button);
        }

        return BuildTabBody(
            T("Themes", "Темы", "Themen"),
            T(
                "Pick the whole app mood. Themes affect the sidebar, panels, buttons, and editor surfaces.",
                "Выбери настроение приложения. Тема меняет боковую панель, блоки, кнопки и редактор.",
                "Wähle die Stimmung der App. Themen ändern Seitenleiste, Flächen, Buttons und Editor."),
            list);
    }

    private UIElement BuildEditorTab()
    {
        _markerBox.MinWidth = 220;
        _markerBox.Margin = new Thickness(0, 8, 0, 0);
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Purple marker", "Фиолетовый маркер", "Violetter Marker"), Tag = "#D8B4FE" });
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Blue marker", "Синий маркер", "Blauer Marker"), Tag = "#BFDBFE" });
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Green marker", "Зелёный маркер", "Grüner Marker"), Tag = "#BBF7D0" });
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Yellow marker", "Жёлтый маркер", "Gelber Marker"), Tag = "#FEF08A" });
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Gray marker", "Серый маркер", "Grauer Marker"), Tag = "#E5E7EB" });
        foreach (ComboBoxItem item in _markerBox.Items)
        {
            if (Equals(item.Tag, _settings.DefaultMarkerColor))
            {
                _markerBox.SelectedItem = item;
                break;
            }
        }

        _markerBox.SelectedItem ??= _markerBox.Items[0];

        _markerBox.SelectionChanged += (_, _) =>
        {
            if (_markerBox.SelectedItem is ComboBoxItem { Tag: string color })
            {
                _settings.DefaultMarkerColor = color;
            }
        };

        return BuildTabBody(
            T("Editor", "Редактор", "Editor"),
            T(
                "Small defaults for writing and marking text.",
                "Небольшие настройки письма и маркера.",
                "Kleine Standards für Schreiben und Markieren."),
            new StackPanel
            {
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new TextBlock
                    {
                        Text = T("Default marker color", "Цвет маркера по умолчанию", "Standardfarbe des Markers"),
                        Foreground = new SolidColorBrush(_theme.Muted),
                        FontWeight = FontWeights.SemiBold
                    },
                    _markerBox
                }
            });
    }

    private UIElement BuildStorageTab(string storagePath, string? activeWorkspacePath)
    {
        return BuildTabBody(
            T("Storage", "Хранилище", "Speicher"),
            T(
                "A quick look at where the app keeps its library and the current workspace files.",
                "Быстрый взгляд на то, где приложение хранит библиотеку и файлы текущего воркспейса.",
                "Ein kurzer Blick darauf, wo die App Bibliothek und aktuelle Arbeitsbereich-Dateien speichert."),
            new StackPanel
            {
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    BuildPathBlock(T("Library JSON", "JSON библиотеки", "Bibliothek-JSON"), storagePath),
                    BuildPathBlock(
                        T("Active workspace folder", "Папка текущего воркспейса", "Ordner des aktiven Arbeitsbereichs"),
                        activeWorkspacePath ?? T("No workspace selected", "Воркспейс не выбран", "Kein Arbeitsbereich ausgewählt"))
                }
            });
    }

    private UIElement BuildTabBody(string title, string subtitle, UIElement content)
    {
        return new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    Foreground = new SolidColorBrush(_theme.Ink),
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = subtitle,
                    Foreground = new SolidColorBrush(_theme.Muted),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 0)
                },
                content
            }
        };
    }

    private UIElement BuildThemeRow(AppThemeOption option)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var swatches = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 1, 12, 0)
        };
        foreach (var color in new[] { option.Side, option.Panel, option.Accent, option.Warm })
        {
            swatches.Children.Add(new Border
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 0, 4, 0),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(_theme.Line),
                BorderThickness = new Thickness(1)
            });
        }

        row.Children.Add(swatches);
        var label = new TextBlock
        {
            Text = _settings.Language switch
            {
                "ru" => option.RussianName,
                "de" => option.GermanName,
                _ => option.EnglishName
            },
            Foreground = new SolidColorBrush(_theme.Ink),
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        return row;
    }

    private UIElement BuildPathBlock(string label, string value)
    {
        return new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 14),
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Foreground = new SolidColorBrush(_theme.Muted),
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = value,
                    Foreground = new SolidColorBrush(_theme.Ink),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                }
            }
        };
    }

    private UIElement BuildActions()
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16)
        };

        actions.Children.Add(CreateButton(T("Cancel", "Отмена", "Abbrechen"), new SolidColorBrush(_theme.Ghost), new SolidColorBrush(_theme.Ink), (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton(T("Save", "Сохранить", "Speichern"), new SolidColorBrush(_theme.Accent), Brushes.White, (_, _) => DialogResult = true, new Thickness(10, 0, 0, 0)));
        return actions;
    }

    private static Button CreateButton(
        string text,
        Brush background,
        Brush foreground,
        RoutedEventHandler onClick,
        Thickness? margin = null)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 36,
            MinWidth = 96,
            Padding = new Thickness(14, 6, 14, 6),
            BorderThickness = new Thickness(0),
            Background = background,
            Foreground = foreground,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Margin = margin ?? new Thickness(0)
        };
        button.Click += onClick;
        return button;
    }

    private string T(string english, string russian)
    {
        return T(english, russian, english);
    }

    private string T(string english, string russian, string german)
    {
        return _settings.Language switch
        {
            "ru" => russian,
            "de" => german,
            _ => english
        };
    }
}

public sealed class DeleteConfirmationDialog : Window
{
    private static readonly SolidColorBrush InkBrush = new(Color.FromRgb(35, 35, 35));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(108, 104, 97));
    private static readonly SolidColorBrush PanelBrush = new(Color.FromRgb(255, 252, 247));
    private static readonly SolidColorBrush LineBrush = new(Color.FromRgb(216, 208, 196));
    private static readonly SolidColorBrush SideBrush = new(Color.FromRgb(37, 49, 55));
    private static readonly SolidColorBrush WarmBrush = new(Color.FromRgb(169, 95, 71));
    private static readonly SolidColorBrush GhostBrush = new(Color.FromRgb(233, 226, 216));
    private readonly string _language;

    private DeleteConfirmationDialog(string itemType, string itemName, string detail, AppThemeOption theme, string language)
    {
        _language = language;
        Title = T($"Delete {itemType}", $"Удалить {itemType}", $"{itemType} löschen");
        Width = 430;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        AllowsTransparency = true;
        FontFamily = new FontFamily("Segoe UI");
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };

        Content = new Border
        {
            Background = new SolidColorBrush(theme.Panel),
            BorderBrush = new SolidColorBrush(theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent(itemType, itemName, detail)
        };
    }

    public static bool Confirm(Window owner, string itemType, string itemName, string detail, AppThemeOption theme, string language)
    {
        var dialog = new DeleteConfirmationDialog(itemType, itemName, detail, theme, language)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true;
    }

    private UIElement BuildContent(string itemType, string itemName, string detail)
    {
        var root = new DockPanel();

        var header = new Border
        {
            Background = SideBrush,
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(18, 15, 18, 14),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = T($"Delete {itemType}?", $"Удалить {itemType}?", $"{itemType} löschen?"),
                        Foreground = Brushes.White,
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = T("This action needs confirmation.", "Это действие нужно подтвердить.", "Diese Aktion muss bestätigt werden."),
                        Foreground = new SolidColorBrush(Color.FromRgb(184, 196, 201)),
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                }
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var body = new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = T(
                        $"Are you sure you want to delete {itemType} \"{itemName}\"?",
                        $"Точно удалить {itemType} \"{itemName}\"?",
                        $"{itemType} \"{itemName}\" wirklich löschen?"),
                    Foreground = InkBrush,
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = detail,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 18)
                },
                BuildActions()
            }
        };

        root.Children.Add(body);
        return root;
    }

    private UIElement BuildActions()
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        actions.Children.Add(CreateButton(T("Cancel", "Отмена", "Abbrechen"), GhostBrush, InkBrush, (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton(T("Delete", "Удалить", "Löschen"), WarmBrush, Brushes.White, (_, _) => DialogResult = true, new Thickness(10, 0, 0, 0)));
        return actions;
    }

    private string T(string english, string russian)
    {
        return T(english, russian, english);
    }

    private string T(string english, string russian, string german)
    {
        return _language switch
        {
            "ru" => russian,
            "de" => german,
            _ => english
        };
    }

    private static Button CreateButton(
        string text,
        Brush background,
        Brush foreground,
        RoutedEventHandler onClick,
        Thickness? margin = null)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 36,
            MinWidth = 96,
            Padding = new Thickness(14, 6, 14, 6),
            BorderThickness = new Thickness(0),
            Background = background,
            Foreground = foreground,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Margin = margin ?? new Thickness(0)
        };
        button.Click += onClick;
        return button;
    }
}

public sealed class TextPromptDialog : Window
{
    private static readonly SolidColorBrush InkBrush = new(Color.FromRgb(35, 35, 35));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(108, 104, 97));
    private static readonly SolidColorBrush PanelBrush = new(Color.FromRgb(255, 252, 247));
    private static readonly SolidColorBrush LineBrush = new(Color.FromRgb(216, 208, 196));
    private static readonly SolidColorBrush SideBrush = new(Color.FromRgb(37, 49, 55));
    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(49, 91, 122));
    private static readonly SolidColorBrush GhostBrush = new(Color.FromRgb(233, 226, 216));

    private readonly TextBox _input = new();
    private readonly string _language;
    private readonly AppThemeOption _theme;

    private TextPromptDialog(string title, string label, string currentValue, AppThemeOption theme, string language)
    {
        _theme = theme;
        _language = language;
        Title = title;
        Width = 430;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        AllowsTransparency = true;
        FontFamily = new FontFamily("Segoe UI");
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
        Loaded += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };

        Content = new Border
        {
            Background = new SolidColorBrush(theme.Panel),
            BorderBrush = new SolidColorBrush(theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent(title, label, currentValue)
        };
    }

    public static string? Prompt(Window owner, string title, string label, string currentValue, AppThemeOption theme, string language)
    {
        var dialog = new TextPromptDialog(title, label, currentValue, theme, language)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog._input.Text : null;
    }

    private UIElement BuildContent(string title, string label, string currentValue)
    {
        var root = new DockPanel();
        var header = new Border
        {
            Background = SideBrush,
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(18, 15, 18, 14),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = Brushes.White,
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = T("Give it a clear name for the sidebar.", "Дай понятное название для боковой панели.", "Gib einen klaren Namen für die Seitenleiste ein."),
                        Foreground = new SolidColorBrush(Color.FromRgb(184, 196, 201)),
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                }
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        _input.Text = currentValue;
        _input.MinHeight = 38;
        _input.Margin = new Thickness(0, 7, 0, 18);
        _input.Padding = new Thickness(10);
        _input.BorderBrush = LineBrush;
        _input.BorderThickness = new Thickness(1);
        _input.Foreground = new SolidColorBrush(_theme.Ink);
        _input.Background = new SolidColorBrush(_theme.Input);
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
            }
        };

        root.Children.Add(new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Foreground = MutedBrush,
                    FontWeight = FontWeights.SemiBold
                },
                _input,
                BuildActions()
            }
        });

        return root;
    }

    private UIElement BuildActions()
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        actions.Children.Add(CreateButton(T("Cancel", "Отмена", "Abbrechen"), GhostBrush, InkBrush, (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton(T("Save", "Сохранить", "Speichern"), AccentBrush, Brushes.White, (_, _) => DialogResult = true, new Thickness(10, 0, 0, 0)));
        return actions;
    }

    private string T(string english, string russian)
    {
        return T(english, russian, english);
    }

    private string T(string english, string russian, string german)
    {
        return _language switch
        {
            "ru" => russian,
            "de" => german,
            _ => english
        };
    }

    private static Button CreateButton(
        string text,
        Brush background,
        Brush foreground,
        RoutedEventHandler onClick,
        Thickness? margin = null)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 36,
            MinWidth = 96,
            Padding = new Thickness(14, 6, 14, 6),
            BorderThickness = new Thickness(0),
            Background = background,
            Foreground = foreground,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Margin = margin ?? new Thickness(0)
        };
        button.Click += onClick;
        return button;
    }
}

public sealed class FlowStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public FlowStorage()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThoughtFlow");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "library.json");
    }

    public string DisplayPath => _path;

    public FlowLibrary Load()
    {
        if (!File.Exists(_path))
        {
            return new FlowLibrary();
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<FlowLibrary>(json) ?? new FlowLibrary();
    }

    public void Save(FlowLibrary library)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(library, JsonOptions));
    }
}

public sealed class FlowLibrary
{
    public FlowAppSettings Settings { get; set; } = new();
    public ObservableCollection<FlowChannel> Channels { get; set; } = [];

    public void Normalize()
    {
        Settings ??= new FlowAppSettings();
        Settings.Normalize();
        Channels ??= [];
        foreach (var channel in Channels)
        {
            channel.Normalize();
        }
    }

    public static FlowLibrary CreateStarter()
    {
        return new FlowLibrary
        {
            Channels =
            [
                new FlowChannel
                {
                    Name = "writing-room",
                    Files =
                    [
                        new FlowTextFile
                        {
                            Name = "main",
                            Messages =
                            [
                                new FlowMessage
                                {
                                    Body = "A workspace holds files. A file is written as a stream of message chunks."
                                },
                                new FlowMessage
                                {
                                    Body = "The stream reads like one continuous text, but each message can still be opened and edited on the right."
                                }
                            ]
                        },
                        new FlowTextFile
                        {
                            Name = "ideas",
                            Messages =
                            [
                                new FlowMessage
                                {
                                    Body = "Possible next ideas: rename files, export one file to Markdown, drag messages between files, and a clean focus mode."
                                }
                            ]
                        }
                    ]
                },
                new FlowChannel
                {
                    Name = "scenes",
                    Files =
                    [
                        new FlowTextFile
                        {
                            Name = "dialogue draft",
                            Messages =
                            [
                                new FlowMessage
                                {
                                    Body = "A scene can start as chat-like fragments, then become prose when the pieces finally know where they belong."
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }
}

public sealed class FlowAppSettings
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "paper-blue";
    public string DefaultMarkerColor { get; set; } = "#D8B4FE";

    public void Normalize()
    {
        Language = (Language ?? string.Empty).ToLowerInvariant() switch
        {
            "ru" => "ru",
            "de" => "de",
            _ => "en"
        };
        if (string.IsNullOrWhiteSpace(Theme))
        {
            Theme = "paper-blue";
        }

        if (string.IsNullOrWhiteSpace(DefaultMarkerColor) ||
            ColorConverter.ConvertFromString(DefaultMarkerColor) is not Color)
        {
            DefaultMarkerColor = "#D8B4FE";
        }
    }

    public FlowAppSettings Clone()
    {
        return new FlowAppSettings
        {
            Language = Language,
            Theme = Theme,
            DefaultMarkerColor = DefaultMarkerColor
        };
    }
}

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

public sealed class FlowChannel : INotifyPropertyChanged
{
    private string _name = "untitled";
    private string _locationPath = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public string LocationPath
    {
        get => _locationPath;
        set
        {
            if (_locationPath == value)
            {
                return;
            }

            _locationPath = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<FlowTextFile> Files { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObservableCollection<FlowMessage>? Messages { get; set; }

    public string CountText => $"{Files.Count} files - {MessageCount} messages";
    public int MessageCount => Files.Sum(file => file.Messages.Count);
    public int WordCount => Files.Sum(file => file.WordCount);

    public void Normalize()
    {
        Files ??= [];

        if (Files.Count == 0)
        {
            Files.Add(new FlowTextFile
            {
                Name = "main",
                Messages = Messages is { Count: > 0 } ? Messages : []
            });
        }

        Messages = null;
        if (string.IsNullOrWhiteSpace(LocationPath))
        {
            LocationPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ThoughtFlow",
                "Workspaces",
                MakeSafeFolderName(Name));
        }

        foreach (var file in Files)
        {
            file.Normalize();
        }
    }

    public void NotifyCountChanged()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(MessageCount));
        OnPropertyChanged(nameof(WordCount));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string MakeSafeFolderName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = string.Concat(value.Select(character => invalid.Contains(character) ? '-' : character)).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "untitled" : safe;
    }
}

public sealed class FlowTextFile : INotifyPropertyChanged
{
    private string _name = "main";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<FlowMessage> Messages { get; set; } = [];

    public string CountText => $"{Messages.Count} messages";
    public int WordCount => Messages.Sum(message => message.WordCount);

    public void Normalize()
    {
        Messages ??= [];
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = "main";
        }
    }

    public void NotifyCountChanged()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(WordCount));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class FlowMessage : INotifyPropertyChanged
{
    private string _body = string.Empty;
    private string _documentXaml = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Author { get; set; } = "Me";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Body
    {
        get => _body;
        set
        {
            if (_body == value)
            {
                return;
            }

            _body = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(WordCount));
            OnPropertyChanged(nameof(WordCountText));
        }
    }

    public string DocumentXaml
    {
        get => _documentXaml;
        set
        {
            if (_documentXaml == value)
            {
                return;
            }

            _documentXaml = value;
            OnPropertyChanged();
        }
    }

    public string TimestampText => CreatedAt.ToString("MMM d, HH:mm");

    public string Preview
    {
        get
        {
            var compact = Body.ReplaceLineEndings(" ").Trim();
            return compact.Length <= 240 ? compact : string.Concat(compact.AsSpan(0, 240), "...");
        }
    }

    public int WordCount => Body.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    public string WordCountText => $"{WordCount} words";

    public FlowMessage CloneAsDraft()
    {
        return new FlowMessage
        {
            Author = Author,
            Body = Body,
            DocumentXaml = DocumentXaml,
            CreatedAt = DateTime.Now
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


