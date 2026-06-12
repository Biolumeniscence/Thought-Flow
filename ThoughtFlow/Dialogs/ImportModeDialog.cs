using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ThoughtFlow;

public enum ImportMode
{
    Workspace,
    File,
    CurrentText
}

public sealed class ImportModeDialog : Window
{
    private readonly AppThemeOption _theme;
    private readonly string _language;
    private readonly bool _canImportFile;
    private readonly bool _canImportCurrentText;

    private ImportModeDialog(AppThemeOption theme, string language, bool canImportFile, bool canImportCurrentText)
    {
        _theme = theme;
        _language = language;
        _canImportFile = canImportFile;
        _canImportCurrentText = canImportCurrentText;
        Title = T("Import", "Импорт", "Import");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        AllowsTransparency = true;
        FontFamily = new FontFamily("Segoe UI");
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };

        Content = new Border
        {
            Background = new SolidColorBrush(theme.Panel),
            BorderBrush = new SolidColorBrush(theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent()
        };
    }

    public ImportMode Mode { get; private set; } = ImportMode.CurrentText;

    public static ImportMode? Prompt(Window owner, AppThemeOption theme, string language, bool canImportFile, bool canImportCurrentText)
    {
        var dialog = new ImportModeDialog(theme, language, canImportFile, canImportCurrentText)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.Mode : null;
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel();
        var header = new Border
        {
            Background = new SolidColorBrush(_theme.Side),
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(18, 15, 18, 14),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = T("Import", "Импорт", "Import"),
                        Foreground = new SolidColorBrush(_theme.SideText),
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = T(
                            "Choose what should be created from the source.",
                            "Выбери, что создать из источника.",
                            "Wähle, was aus der Quelle erstellt werden soll."),
                        Foreground = new SolidColorBrush(_theme.SideMuted),
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                }
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var body = new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                CreateModeButton(
                    ImportMode.Workspace,
                    T("Import workspace", "Импортировать воркспейс", "Arbeitsbereich importieren"),
                    T("Choose a folder. Each supported document inside becomes a file.", "Выбрать папку. Каждый поддерживаемый документ внутри станет файлом.", "Ordner wählen. Jedes unterstützte Dokument darin wird eine Datei."),
                    true),
                CreateModeButton(
                    ImportMode.File,
                    T("Import file", "Импортировать файл", "Datei importieren"),
                    T("Create a new file in the current workspace.", "Создать новый файл в текущем воркспейсе.", "Neue Datei im aktuellen Arbeitsbereich erstellen."),
                    _canImportFile),
                CreateModeButton(
                    ImportMode.CurrentText,
                    T("Import into current text", "Импортировать в текущий текст", "In aktuellen Text importieren"),
                    T("Append one document to the selected file as messages.", "Добавить один документ в выбранный файл сообщениями.", "Ein Dokument als Nachrichten an die ausgewählte Datei anhängen."),
                    _canImportCurrentText),
                BuildActions()
            }
        };

        root.Children.Add(body);
        return root;
    }

    private Button CreateModeButton(ImportMode mode, string title, string detail, bool isEnabled)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(_theme.Ink)
        };
        var detailBlock = new TextBlock
        {
            Text = detail,
            Foreground = new SolidColorBrush(_theme.Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        };

        var button = new Button
        {
            Content = new StackPanel
            {
                Children =
                {
                    titleBlock,
                    detailBlock
                }
            },
            IsEnabled = isEnabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(_theme.Input),
            Foreground = new SolidColorBrush(_theme.Ink),
            BorderBrush = new SolidColorBrush(_theme.Line),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10),
            Cursor = Cursors.Hand
        };
        button.Click += (_, _) =>
        {
            Mode = mode;
            DialogResult = true;
        };
        return button;
    }

    private UIElement BuildActions()
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };

        actions.Children.Add(CreateButton(T("Cancel", "Отмена", "Abbrechen"), new SolidColorBrush(_theme.Ghost), new SolidColorBrush(_theme.Ink), (_, _) => DialogResult = false));
        return actions;
    }

    private string T(string english, string russian, string german)
    {
        return _language switch
        {
            "ru" => russian,
            "de" => german,
            _ => english
        };
    }

    private static Button CreateButton(
        string text,
        Brush background,
        Brush foreground,
        RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 34,
            MinWidth = 90,
            Padding = new Thickness(12, 6, 12, 6),
            BorderThickness = new Thickness(0),
            Background = background,
            Foreground = foreground,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        button.Click += onClick;
        return button;
    }
}
