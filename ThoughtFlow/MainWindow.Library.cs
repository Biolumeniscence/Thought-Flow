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
{    private void LoadLibrary()
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
}

