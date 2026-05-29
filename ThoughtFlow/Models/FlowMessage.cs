using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ThoughtFlow;

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

