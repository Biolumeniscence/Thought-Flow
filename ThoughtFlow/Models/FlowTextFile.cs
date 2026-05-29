using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace ThoughtFlow;

public sealed class FlowTextFile : INotifyPropertyChanged
{
    private string _name = "main";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

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

    public ObservableCollection<FlowMessage> Messages { get; set; } = [];

    public string CountText => $"{Messages.Count} messages";
    public int WordCount => Messages.Sum(message => message.WordCount);

    public void Normalize()
    {
        Messages ??= [];
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = "main";
        }
    }

    public void NotifyCountChanged()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(WordCount));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

