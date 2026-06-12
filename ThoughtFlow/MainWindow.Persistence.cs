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
{    private string? SaveWorkspaceFiles(FlowLibrary library)
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
                    File.WriteAllText(path, TextExportService.BuildPlainText(file));
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
}

