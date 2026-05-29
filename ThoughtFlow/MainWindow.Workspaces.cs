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
{    private void WorkspaceItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
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
}

