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
{    private void ChannelsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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
}

