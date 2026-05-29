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
{    private void RefreshEditorLabels()
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
}

