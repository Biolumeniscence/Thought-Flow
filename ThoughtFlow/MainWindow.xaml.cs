using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ThoughtFlow;

public partial class MainWindow : Window
{
    private static readonly Color InkColor = Color.FromRgb(35, 35, 35);
    private static readonly Color DefaultMarkColor = Color.FromRgb(216, 180, 254);
    private static readonly Color SpoilerHiddenColor = Color.FromRgb(49, 49, 54);
    private static readonly Color SpoilerOpenColor = Color.FromRgb(229, 231, 235);

    private readonly ObservableCollection<FlowChannel> _channels = [];
    private readonly ObservableCollection<FlowTextFile> _files = [];
    private readonly List<FlowMessage> _visibleMessages = [];
    private readonly Dictionary<Paragraph, FlowMessage> _streamParagraphMessages = [];
    private readonly HashSet<string> _openSpoilers = [];
    private readonly FlowStorage _storage = new();
    private Color _selectedMarkColor = DefaultMarkColor;

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
        StoragePathText.Text = _storage.DisplayPath;

        LoadLibrary();
        ChannelsList.ItemsSource = _channels;
        FilesList.ItemsSource = _files;
        ChannelsList.SelectedIndex = 0;
    }

    private void LoadLibrary()
    {
        var library = _storage.Load();
        library.Normalize();

        if (library.Channels.Count == 0)
        {
            library = FlowLibrary.CreateStarter();
            library.Normalize();
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
        return new FlowLibrary { Channels = [.. _channels] };
    }

    private void SaveLibrary()
    {
        var snapshot = Snapshot();
        _storage.Save(snapshot);
        var syncError = SaveWorkspaceFiles(snapshot);
        SaveStatusText.Text = syncError is null
            ? $"Saved {DateTime.Now:HH:mm:ss}"
            : $"Saved, sync issue: {syncError}";
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
            ChannelSubtitle.Text = "Create a workspace to start writing.";
            return;
        }

        ChannelTitle.Text = $"#{_activeChannel.Name}";
        ChannelSubtitle.Text = _activeFile is null
            ? $"{_activeChannel.Files.Count} files"
            : $"{_activeFile.Name} - {_activeFile.Messages.Count} messages, {_activeFile.WordCount} words - {_activeChannel.LocationPath}";
    }

    private void RefreshMessages()
    {
        ClearHoveredStreamParagraph(force: true);
        _visibleMessages.Clear();
        _streamParagraphMessages.Clear();

        var document = CreateStreamDocument();

        if (_activeFile is null)
        {
            document.Blocks.Add(CreateHintParagraph("Create or select a file inside this workspace."));
            MessageStreamBox.Document = document;
            SearchStatsText.Text = "0 messages";
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
            document.Blocks.Add(CreateHintParagraph("No messages here yet."));
        }

        MessageStreamBox.Document = document;
        SearchStatsText.Text = $"{_visibleMessages.Count} of {_activeFile.Messages.Count}";
    }

    private static FlowDocument CreateStreamDocument()
    {
        return new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize = 15,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(InkColor)
        };
    }

    private static Paragraph CreateHintParagraph(string text)
    {
        return new Paragraph(new Run(text))
        {
            Foreground = new SolidColorBrush(Color.FromRgb(108, 104, 97)),
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
            AppendRuns(paragraph.Inlines, segment.Text, segment.Style, new SolidColorBrush(InkColor), CreateBrush(segment.MarkColor));
            return;
        }

        var isOpen = _openSpoilers.Contains(segment.SpoilerKey);
        var span = new Span
        {
            Tag = segment.SpoilerKey,
            Cursor = Cursors.Hand,
            ToolTip = isOpen ? "Click to hide spoiler" : "Click to reveal spoiler"
        };
        span.MouseLeftButtonUp += SpoilerSpan_MouseLeftButtonUp;

        var foreground = new SolidColorBrush(isOpen ? InkColor : SpoilerHiddenColor);
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
        _hoveredStreamParagraph.Background = new SolidColorBrush(Color.FromRgb(232, 229, 224));
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
            $"Message deleted: \"{messageTitle}\"",
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
            EditorStateText.Text = "Copied";
        }
    }

    private void CopyMessageText(FlowMessage message)
    {
        Clipboard.SetText(message.Body);
        EditorStateText.Text = "Copied";
    }

    private void MarkButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorBackground(_selectedMarkColor, "Marked");
    }

    private void ClearMarkButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorBackground(null, "Mark cleared");
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

    private void ApplyEditorBackground(Color? color, string stateText)
    {
        if (!EditorBox.IsEnabled)
        {
            return;
        }

        if (EditorBox.Selection.IsEmpty)
        {
            EditorStateText.Text = "Select text first";
            return;
        }

        EditorBox.Focus();
        var brush = color is null ? Brushes.Transparent : new SolidColorBrush(color.Value);
        EditorBox.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
        _hasUnsavedEditorText = true;
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
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Rename", RenameWorkspaceMenuItem_Click));
        menu.Items.Add(CreateMenuItem("Duplicate", DuplicateWorkspaceMenuItem_Click));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Delete", DeleteWorkspaceMenuItem_Click));
        return menu;
    }

    private ContextMenu BuildFileContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Rename", RenameFileMenuItem_Click));
        menu.Items.Add(CreateMenuItem("Duplicate", DuplicateFileMenuItem_Click));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Delete", DeleteFileMenuItem_Click));
        return menu;
    }

    private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        return item;
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
        var name = NormalizeWorkspaceName(NewChannelBox.Text);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var location = ChooseWorkspaceLocation(name);
        if (location is null)
        {
            return;
        }

        var channel = new FlowChannel { Name = name, LocationPath = location };
        channel.Files.Add(new FlowTextFile { Name = "main" });
        AttachChannel(channel);
        _channels.Add(channel);
        NewChannelBox.Clear();
        ChannelsList.SelectedItem = channel;
        SaveLibrary();
    }

    private string? ChooseWorkspaceLocation(string workspaceName)
    {
        var dialog = new OpenFolderDialog
        {
            Title = $"Choose a folder for workspace \"{workspaceName}\"",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return null;
        }

        Directory.CreateDirectory(dialog.FolderName);
        return dialog.FolderName;
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
        var name = TextPromptDialog.Prompt(this, "Rename workspace", "Workspace name", workspace.Name);
        if (name is null)
        {
            return;
        }

        var normalized = NormalizeWorkspaceName(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        workspace.Name = normalized;
        RefreshHeader();
        SaveLibrary();
    }

    private void DuplicateWorkspace(FlowChannel workspace)
    {
        var duplicateName = MakeUniqueWorkspaceName($"{workspace.Name}-copy");
        var location = ChooseWorkspaceLocation(duplicateName);
        if (location is null)
        {
            return;
        }

        var duplicate = CloneWorkspace(workspace, duplicateName, location);
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
        var name = TextPromptDialog.Prompt(this, "Rename file", "File name", file.Name);
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
            SaveStatusText.Text = "Keep at least one file";
            return;
        }

        SaveEditorIfDirty();
        if (!ConfirmDelete("file", fileToDelete.Name, "This will remove the file and every message inside it."))
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
            $"File \"{fileToDelete.Name}\" deleted.",
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
            SaveStatusText.Text = "Keep at least one workspace";
            return;
        }

        SaveEditorIfDirty();

        if (!ConfirmDelete("workspace", channelToDelete.Name, "This will remove the workspace, its files, and its messages."))
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
            $"Workspace \"{channelToDelete.Name}\" deleted.",
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
        return DeleteConfirmationDialog.Confirm(this, itemType, itemName, detail);
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
            Background = new SolidColorBrush(Color.FromRgb(49, 64, 71)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(75, 93, 100)),
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
            Foreground = new SolidColorBrush(Color.FromRgb(247, 240, 232)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 9, 0, 0)
        };

        var undoButton = CreateUndoToastButton("Undo", new SolidColorBrush(Color.FromRgb(49, 91, 122)), Brushes.White);
        undoButton.Click += (_, _) =>
        {
            restoreAction();
            RemoveCard();
        };

        var hideButton = CreateUndoToastButton("Hide", new SolidColorBrush(Color.FromRgb(233, 226, 216)), new SolidColorBrush(Color.FromRgb(35, 35, 35)));
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
        EditorStateText.Text = "Unsaved";
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
        EditorStateText.Text = "Saved";
        SaveLibrary();
    }

    private void RefreshEditor()
    {
        _isLoadingEditor = true;
        _hasUnsavedEditorText = false;

        if (_activeMessage is null)
        {
            EditorHint.Text = "Click a message in the stream to edit that message here.";
            EditorStateText.Text = string.Empty;
            EditorBox.Document = CreateDocument(string.Empty);
            EditorBox.IsEnabled = false;
            _isLoadingEditor = false;
            return;
        }

        EditorHint.Text = $"{_activeMessage.TimestampText} - {_activeMessage.WordCountText}";
        EditorStateText.Text = "Saved";
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

public sealed class DeleteConfirmationDialog : Window
{
    private static readonly SolidColorBrush InkBrush = new(Color.FromRgb(35, 35, 35));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(108, 104, 97));
    private static readonly SolidColorBrush PanelBrush = new(Color.FromRgb(255, 252, 247));
    private static readonly SolidColorBrush LineBrush = new(Color.FromRgb(216, 208, 196));
    private static readonly SolidColorBrush SideBrush = new(Color.FromRgb(37, 49, 55));
    private static readonly SolidColorBrush WarmBrush = new(Color.FromRgb(169, 95, 71));
    private static readonly SolidColorBrush GhostBrush = new(Color.FromRgb(233, 226, 216));

    private DeleteConfirmationDialog(string itemType, string itemName, string detail)
    {
        Title = $"Delete {itemType}";
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
            Background = PanelBrush,
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent(itemType, itemName, detail)
        };
    }

    public static bool Confirm(Window owner, string itemType, string itemName, string detail)
    {
        var dialog = new DeleteConfirmationDialog(itemType, itemName, detail)
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
                        Text = $"Delete {itemType}?",
                        Foreground = Brushes.White,
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "This action needs confirmation.",
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
                    Text = $"Are you sure you want to delete {itemType} \"{itemName}\"?",
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

        actions.Children.Add(CreateButton("Cancel", GhostBrush, InkBrush, (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton("Delete", WarmBrush, Brushes.White, (_, _) => DialogResult = true, new Thickness(10, 0, 0, 0)));
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

    private TextPromptDialog(string title, string label, string currentValue)
    {
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
            Background = PanelBrush,
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent(title, label, currentValue)
        };
    }

    public static string? Prompt(Window owner, string title, string label, string currentValue)
    {
        var dialog = new TextPromptDialog(title, label, currentValue)
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
                        Text = "Give it a clear name for the sidebar.",
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
        _input.Foreground = InkBrush;
        _input.Background = Brushes.White;
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

        actions.Children.Add(CreateButton("Cancel", GhostBrush, InkBrush, (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton("Save", AccentBrush, Brushes.White, (_, _) => DialogResult = true, new Thickness(10, 0, 0, 0)));
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
    public ObservableCollection<FlowChannel> Channels { get; set; } = [];

    public void Normalize()
    {
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
