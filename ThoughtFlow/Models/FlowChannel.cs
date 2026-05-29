using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Linq;

namespace ThoughtFlow;

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

