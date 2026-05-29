using System.Windows.Media;

namespace ThoughtFlow;

public sealed class FlowAppSettings
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "paper-blue";
    public string DefaultMarkerColor { get; set; } = "#D8B4FE";

    public void Normalize()
    {
        Language = (Language ?? string.Empty).ToLowerInvariant() switch
        {
            "ru" => "ru",
            "de" => "de",
            _ => "en"
        };
        if (string.IsNullOrWhiteSpace(Theme))
        {
            Theme = "paper-blue";
        }

        if (string.IsNullOrWhiteSpace(DefaultMarkerColor) ||
            ColorConverter.ConvertFromString(DefaultMarkerColor) is not Color)
        {
            DefaultMarkerColor = "#D8B4FE";
        }
    }

    public FlowAppSettings Clone()
    {
        return new FlowAppSettings
        {
            Language = Language,
            Theme = Theme,
            DefaultMarkerColor = DefaultMarkerColor
        };
    }
}

