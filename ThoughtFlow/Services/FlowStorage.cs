using System.IO;
using System.Text.Json;

namespace ThoughtFlow;

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

