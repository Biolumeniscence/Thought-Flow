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
{    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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
        NormalizeDocumentMarkerColors(EditorBox.Document, forStorage: false);
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
        NormalizeDocumentMarkerColors(EditorBox.Document, forStorage: true);
        var xaml = XamlWriter.Save(EditorBox.Document);
        NormalizeDocumentMarkerColors(EditorBox.Document, forStorage: false);
        return xaml;
    }

    private void NormalizeDocumentMarkerColors(FlowDocument document, bool forStorage)
    {
        var documentBackground = document.Background;
        NormalizeBrush(ref documentBackground, forStorage);
        document.Background = documentBackground;

        foreach (var block in document.Blocks)
        {
            NormalizeBlockMarkerColors(block, forStorage);
        }
    }

    private void NormalizeBlockMarkerColors(Block block, bool forStorage)
    {
        var background = block.Background;
        NormalizeBrush(ref background, forStorage);
        block.Background = background;

        if (block is Paragraph paragraph)
        {
            foreach (var inline in paragraph.Inlines)
            {
                NormalizeInlineMarkerColors(inline, forStorage);
            }
        }
    }

    private void NormalizeInlineMarkerColors(Inline inline, bool forStorage)
    {
        var background = inline.Background;
        NormalizeBrush(ref background, forStorage);
        inline.Background = background;

        if (inline is Span span)
        {
            foreach (var child in span.Inlines)
            {
                NormalizeInlineMarkerColors(child, forStorage);
            }
        }
    }

    private void NormalizeBrush(ref Brush? brush, bool forStorage)
    {
        if (!TryGetMarkColor(brush, out var color))
        {
            return;
        }

        var storageColor = MarkerPalette.ToStorageColor(color);
        var displayColor = forStorage ? storageColor : GetMarkerDisplayColor(storageColor);
        brush = new SolidColorBrush(displayColor);
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

}

