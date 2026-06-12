using System.IO;
using Microsoft.Win32;
using System.Windows;

namespace ThoughtFlow;

public partial class MainWindow
{
    private const string ImportFileFilter = "Text and Word documents (*.txt;*.docx)|*.txt;*.docx|Text files (*.txt)|*.txt|Word documents (*.docx)|*.docx";

    private void UpdateFileTransferButtons()
    {
        ImportFileButton.IsEnabled = true;
        ExportFileButton.IsEnabled = _activeFile is not null;
    }

    private void ImportFileButton_Click(object sender, RoutedEventArgs e)
    {
        ShowImportModeDialog(_activeFile);
    }

    private void ExportFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeFile is { } file)
        {
            ExportTextFile(file);
        }
    }

    private void ShowImportModeDialog(FlowTextFile? contextFile)
    {
        var mode = ImportModeDialog.Prompt(
            this,
            _currentTheme,
            _settings.Language,
            _activeChannel is not null,
            contextFile is not null);

        switch (mode)
        {
            case ImportMode.Workspace:
                ImportWorkspaceFromFolder();
                break;
            case ImportMode.File:
                ImportTextAsNewFile();
                break;
            case ImportMode.CurrentText when contextFile is not null:
                ImportTextIntoExistingFile(contextFile);
                break;
        }
    }

    private void ImportWorkspaceFromFolder()
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = L("Choose a folder to import as a workspace", "Выбери папку для импорта как воркспейс", "Ordner als Arbeitsbereich importieren"),
            Multiselect = false
        };

        if (folderDialog.ShowDialog(this) != true)
        {
            return;
        }

        var sourceFiles = Directory
            .EnumerateFiles(folderDialog.FolderName)
            .Where(TextImportService.IsSupportedImportFile)
            .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (sourceFiles.Count == 0)
        {
            SaveStatusText.Text = L(
                "No .txt or .docx files found in that folder.",
                "В этой папке не найдено .txt или .docx файлов.",
                "In diesem Ordner wurden keine .txt- oder .docx-Dateien gefunden.");
            return;
        }

        var options = ImportTextDialog.Prompt(this, _currentTheme, _settings.Language);
        if (options is null)
        {
            return;
        }

        SaveEditorIfDirty();

        var folderName = NormalizeImportedName(new DirectoryInfo(folderDialog.FolderName).Name, "workspace");
        var workspace = new FlowChannel
        {
            Name = MakeUniqueWorkspaceName(folderName),
            LocationPath = folderDialog.FolderName
        };
        var skippedFiles = 0;

        foreach (var sourceFile in sourceFiles)
        {
            try
            {
                var importedFile = CreateImportedFlowFile(
                    sourceFile,
                    options,
                    workspace.Files.Select(file => file.Name),
                    out _);

                if (importedFile is null)
                {
                    skippedFiles++;
                    continue;
                }

                workspace.Files.Add(importedFile);
            }
            catch
            {
                skippedFiles++;
            }
        }

        if (workspace.Files.Count == 0)
        {
            SaveStatusText.Text = L(
                "No readable text found in the selected folder.",
                "В выбранной папке не найден читаемый текст.",
                "Im ausgewählten Ordner wurde kein lesbarer Text gefunden.");
            return;
        }

        AttachChannel(workspace);
        _channels.Add(workspace);
        ChannelsList.SelectedItem = workspace;
        FilesList.SelectedItem = workspace.Files.FirstOrDefault();
        SelectFile(workspace.Files.FirstOrDefault());
        SaveLibrary();

        SaveStatusText.Text = L(
            $"Imported workspace \"{workspace.Name}\" with {workspace.Files.Count} files.",
            $"Импортирован воркспейс \"{workspace.Name}\" с файлами: {workspace.Files.Count}.",
            $"Arbeitsbereich \"{workspace.Name}\" mit {workspace.Files.Count} Dateien importiert.") +
            (skippedFiles > 0
                ? L($" Skipped empty/unreadable files: {skippedFiles}.", $" Пропущено пустых или нечитаемых файлов: {skippedFiles}.", $" Leere/unlesbare Dateien übersprungen: {skippedFiles}.")
                : string.Empty);
    }

    private void ImportTextAsNewFile()
    {
        if (_activeChannel is null)
        {
            return;
        }

        var sourcePath = ChooseImportSourceFile(_activeChannel.LocationPath, L("Import file", "Импорт файла", "Datei importieren"));
        if (sourcePath is null)
        {
            return;
        }

        var options = ImportTextDialog.Prompt(this, _currentTheme, _settings.Language);
        if (options is null)
        {
            return;
        }

        try
        {
            SaveEditorIfDirty();
            var importedFile = CreateImportedFlowFile(
                sourcePath,
                options,
                _activeChannel.Files.Select(file => file.Name),
                out var chunkCount);

            if (importedFile is null)
            {
                SaveStatusText.Text = L(
                    "No readable text found in the selected file.",
                    "В выбранном файле не найден читаемый текст.",
                    "In der ausgewählten Datei wurde kein lesbarer Text gefunden.");
                return;
            }

            _activeChannel.Files.Add(importedFile);
            FilesList.SelectedItem = importedFile;
            SelectFile(importedFile);
            _activeMessage = importedFile.Messages.FirstOrDefault();
            RefreshEditor();
            SaveLibrary();

            SaveStatusText.Text = L(
                $"Imported file \"{importedFile.Name}\" as {chunkCount} messages.",
                $"Импортирован файл \"{importedFile.Name}\" как сообщений: {chunkCount}.",
                $"Datei \"{importedFile.Name}\" als {chunkCount} Nachrichten importiert.");
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = L(
                $"Import failed: {ex.Message}",
                $"Импорт не удался: {ex.Message}",
                $"Import fehlgeschlagen: {ex.Message}");
        }
    }

    private void ImportTextIntoExistingFile(FlowTextFile file)
    {
        SelectFileForTransfer(file);

        var channel = FindChannelForFile(file);
        var sourcePath = ChooseImportSourceFile(channel?.LocationPath, L("Import into current text", "Импорт в текущий текст", "In aktuellen Text importieren"));
        if (sourcePath is null)
        {
            return;
        }

        var options = ImportTextDialog.Prompt(this, _currentTheme, _settings.Language);
        if (options is null)
        {
            return;
        }

        try
        {
            SaveEditorIfDirty();
            var chunks = ReadImportChunks(sourcePath, options);
            if (chunks.Count == 0)
            {
                SaveStatusText.Text = L(
                    "No readable text found in the selected file.",
                    "В выбранном файле не найден читаемый текст.",
                    "In der ausgewählten Datei wurde kein lesbarer Text gefunden.");
                return;
            }

            FlowMessage? lastImportedMessage = null;
            foreach (var chunk in chunks)
            {
                lastImportedMessage = CreateMessageFromImportedChunk(chunk);
                file.Messages.Add(lastImportedMessage);
            }

            if (lastImportedMessage is not null)
            {
                _activeMessage = lastImportedMessage;
            }

            RefreshHeader();
            RefreshMessages();
            RefreshEditor();
            MessageStreamBox.ScrollToEnd();
            SaveLibrary();
            SaveStatusText.Text = L(
                $"Imported {chunks.Count} messages from {Path.GetFileName(sourcePath)}.",
                $"Импортировано сообщений: {chunks.Count} из {Path.GetFileName(sourcePath)}.",
                $"{chunks.Count} Nachrichten aus {Path.GetFileName(sourcePath)} importiert.");
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = L(
                $"Import failed: {ex.Message}",
                $"Импорт не удался: {ex.Message}",
                $"Import fehlgeschlagen: {ex.Message}");
        }
    }

    private string? ChooseImportSourceFile(string? initialDirectory, string title)
    {
        var fileDialog = new OpenFileDialog
        {
            Title = title,
            Filter = ImportFileFilter,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            fileDialog.InitialDirectory = initialDirectory;
        }

        return fileDialog.ShowDialog(this) == true ? fileDialog.FileName : null;
    }

    private FlowTextFile? CreateImportedFlowFile(
        string sourcePath,
        TextImportOptions options,
        IEnumerable<string> existingNames,
        out int chunkCount)
    {
        var chunks = ReadImportChunks(sourcePath, options);
        chunkCount = chunks.Count;
        if (chunks.Count == 0)
        {
            return null;
        }

        var importedName = NormalizeImportedName(Path.GetFileNameWithoutExtension(sourcePath), "imported");
        var file = new FlowTextFile
        {
            Name = MakeUniqueName(importedName, existingNames)
        };

        foreach (var chunk in chunks)
        {
            file.Messages.Add(CreateMessageFromImportedChunk(chunk));
        }

        return file;
    }

    private static IReadOnlyList<string> ReadImportChunks(string sourcePath, TextImportOptions options)
    {
        var text = TextImportService.ReadText(sourcePath);
        return TextImportService.SplitIntoMessages(text, options);
    }

    private static FlowMessage CreateMessageFromImportedChunk(string chunk)
    {
        return new FlowMessage
        {
            Body = chunk,
            DocumentXaml = CreateDocumentXaml(chunk)
        };
    }

    private void ExportTextFile(FlowTextFile file)
    {
        SelectFileForTransfer(file);
        SaveEditorIfDirty();

        var safeName = MakeSafeFileName(file.Name, new HashSet<string>(StringComparer.CurrentCultureIgnoreCase));
        var fileDialog = new SaveFileDialog
        {
            Title = L("Export text", "Экспорт текста", "Text exportieren"),
            FileName = $"{safeName}.txt",
            DefaultExt = ".txt",
            AddExtension = true,
            OverwritePrompt = true,
            Filter = "Text file (*.txt)|*.txt|Markdown file (*.md)|*.md|All files (*.*)|*.*"
        };

        var channel = FindChannelForFile(file);
        if (channel is not null && Directory.Exists(channel.LocationPath))
        {
            fileDialog.InitialDirectory = channel.LocationPath;
        }

        if (fileDialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(fileDialog.FileName, TextExportService.BuildPlainText(file));
            SaveStatusText.Text = L(
                $"Exported {file.Messages.Count} messages to {Path.GetFileName(fileDialog.FileName)}.",
                $"Экспортировано сообщений: {file.Messages.Count} в {Path.GetFileName(fileDialog.FileName)}.",
                $"{file.Messages.Count} Nachrichten nach {Path.GetFileName(fileDialog.FileName)} exportiert.");
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = L(
                $"Export failed: {ex.Message}",
                $"Экспорт не удался: {ex.Message}",
                $"Export fehlgeschlagen: {ex.Message}");
        }
    }

    private void SelectFileForTransfer(FlowTextFile file)
    {
        if (ReferenceEquals(_activeFile, file))
        {
            return;
        }

        SaveEditorIfDirty();
        FilesList.SelectedItem = file;
        if (!ReferenceEquals(_activeFile, file))
        {
            SelectFile(file);
        }
    }

    private static string NormalizeImportedName(string name, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = string.Concat(name.Select(character => invalid.Contains(character) ? '-' : character)).Trim();
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
