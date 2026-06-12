using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ThoughtFlow;

public sealed class SettingsDialog : Window
{
    private readonly FlowAppSettings _settings;
    private readonly IReadOnlyList<AppThemeOption> _themes;
    private readonly AppThemeOption _theme;
    private readonly RadioButton _englishButton = new();
    private readonly RadioButton _russianButton = new();
    private readonly RadioButton _germanButton = new();
    private readonly ComboBox _markerBox = new();

    private SettingsDialog(
        FlowAppSettings settings,
        IReadOnlyList<AppThemeOption> themes,
        string storagePath,
        string? activeWorkspacePath)
    {
        _settings = settings;
        _settings.Normalize();
        _themes = themes;
        _theme = themes.FirstOrDefault(theme => theme.Id == settings.Theme) ?? themes[0];

        Title = T("Settings", "Настройки", "Einstellungen");
        Width = 640;
        Height = 520;
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
            Background = new SolidColorBrush(_theme.Panel),
            BorderBrush = new SolidColorBrush(_theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent(storagePath, activeWorkspacePath)
        };
    }

    public static FlowAppSettings? Edit(
        Window owner,
        FlowAppSettings settings,
        IReadOnlyList<AppThemeOption> themes,
        string storagePath,
        string? activeWorkspacePath)
    {
        var dialog = new SettingsDialog(settings, themes, storagePath, activeWorkspacePath)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog._settings : null;
    }

    private UIElement BuildContent(string storagePath, string? activeWorkspacePath)
    {
        var root = new DockPanel();
        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var actions = BuildActions();
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);

        root.Children.Add(CreateSettingsTabs(storagePath, activeWorkspacePath));

        return root;
    }

    private TabControl CreateSettingsTabs(string storagePath, string? activeWorkspacePath)
    {
        var tabs = new TabControl
        {
            Margin = new Thickness(16),
            Background = new SolidColorBrush(_theme.Panel),
            BorderBrush = new SolidColorBrush(_theme.Line),
            Foreground = new SolidColorBrush(_theme.Ink),
            ItemContainerStyle = CreateSettingsTabItemStyle(),
            Template = CreateSettingsTabControlTemplate()
        };

        tabs.Items.Add(new TabItem { Header = T("Language", "Язык", "Sprache"), Content = BuildLanguageTab() });
        tabs.Items.Add(new TabItem { Header = T("Themes", "Темы", "Themen"), Content = BuildThemesTab() });
        tabs.Items.Add(new TabItem { Header = T("Editor", "Редактор", "Editor"), Content = BuildEditorTab() });
        tabs.Items.Add(new TabItem { Header = T("Storage", "Хранилище", "Speicher"), Content = BuildStorageTab(storagePath, activeWorkspacePath) });
        return tabs;
    }

    private ControlTemplate CreateSettingsTabControlTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(_theme.Panel));
        border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(_theme.Line));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

        var dock = new FrameworkElementFactory(typeof(DockPanel));
        border.AppendChild(dock);

        var headerShell = new FrameworkElementFactory(typeof(Border));
        headerShell.SetValue(DockPanel.DockProperty, Dock.Top);
        headerShell.SetValue(Border.BackgroundProperty, new SolidColorBrush(_theme.Ghost));
        headerShell.SetValue(Border.BorderBrushProperty, new SolidColorBrush(_theme.Line));
        headerShell.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
        headerShell.SetValue(Border.CornerRadiusProperty, new CornerRadius(8, 8, 0, 0));
        headerShell.SetValue(Border.PaddingProperty, new Thickness(8, 8, 8, 0));

        var tabPanel = new FrameworkElementFactory(typeof(TabPanel));
        tabPanel.Name = "HeaderPanel";
        tabPanel.SetValue(Panel.IsItemsHostProperty, true);
        headerShell.AppendChild(tabPanel);
        dock.AppendChild(headerShell);

        var contentShell = new FrameworkElementFactory(typeof(Border));
        contentShell.SetValue(Border.BackgroundProperty, new SolidColorBrush(_theme.Panel));
        contentShell.SetValue(Border.CornerRadiusProperty, new CornerRadius(0, 0, 8, 8));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.Name = "PART_SelectedContentHost";
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
        contentShell.AppendChild(presenter);
        dock.AppendChild(contentShell);

        return new ControlTemplate(typeof(TabControl))
        {
            VisualTree = border
        };
    }

    private Style CreateSettingsTabItemStyle()
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(_theme.Muted)));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 8, 14, 8)));
        style.Setters.Add(new Setter(Control.TemplateProperty, CreateSettingsTabItemTemplate()));
        return style;
    }

    private ControlTemplate CreateSettingsTabItemTemplate()
    {
        var shell = new FrameworkElementFactory(typeof(Border));
        shell.Name = "Shell";
        shell.SetValue(Border.BackgroundProperty, new SolidColorBrush(_theme.Ghost));
        shell.SetValue(Border.BorderBrushProperty, new SolidColorBrush(_theme.Line));
        shell.SetValue(Border.BorderThicknessProperty, new Thickness(1, 1, 1, 0));
        shell.SetValue(Border.CornerRadiusProperty, new CornerRadius(7, 7, 0, 0));
        shell.SetValue(Border.MarginProperty, new Thickness(0, 0, 6, 0));
        shell.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        shell.AppendChild(presenter);

        var template = new ControlTemplate(typeof(TabItem))
        {
            VisualTree = shell
        };

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(_theme.AccentSoft), "Shell"));
        hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(_theme.Ink)));
        template.Triggers.Add(hoverTrigger);

        var selectedTrigger = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(_theme.Panel), "Shell"));
        selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(_theme.Accent), "Shell"));
        selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(_theme.Ink)));
        template.Triggers.Add(selectedTrigger);

        return template;
    }

    private UIElement BuildHeader()
    {
        var grid = new Grid
        {
            Background = new SolidColorBrush(_theme.Side)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel
        {
            Margin = new Thickness(18, 15, 18, 14),
            Children =
            {
                new TextBlock
                {
                    Text = T("Settings", "Настройки", "Einstellungen"),
                    Foreground = new SolidColorBrush(_theme.SideText),
                    FontSize = 22,
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = T(
                        "Tune Thought Flow to feel closer to your writing space.",
                        "Настрой Thought Flow под своё рабочее пространство.",
                        "Passe Thought Flow an deinen Schreibraum an."),
                    Foreground = new SolidColorBrush(_theme.SideMuted),
                    Margin = new Thickness(0, 4, 0, 0)
                }
            }
        };
        grid.Children.Add(text);

        var close = CreateCloseButton(new SolidColorBrush(_theme.SideMuted), (_, _) => DialogResult = false);
        close.Width = 40;
        close.MinWidth = 40;
        close.Margin = new Thickness(0, 10, 12, 0);
        close.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(close, 1);
        grid.Children.Add(close);
        return new Border
        {
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Child = grid
        };
    }

    private static Button CreateCloseButton(Brush foreground, RoutedEventHandler onClick)
    {
        var firstStroke = new System.Windows.Shapes.Line
        {
            X1 = 3,
            Y1 = 3,
            X2 = 13,
            Y2 = 13,
            Stroke = foreground,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var secondStroke = new System.Windows.Shapes.Line
        {
            X1 = 13,
            Y1 = 3,
            X2 = 3,
            Y2 = 13,
            Stroke = foreground,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var icon = new Canvas
        {
            Width = 16,
            Height = 16,
            Children =
            {
                firstStroke,
                secondStroke
            }
        };

        var button = new Button
        {
            Content = icon,
            MinHeight = 36,
            MinWidth = 36,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = foreground,
            Cursor = Cursors.Hand
        };
        button.Click += onClick;
        return button;
    }

    private UIElement BuildLanguageTab()
    {
        _englishButton.Content = "English";
        _englishButton.Foreground = new SolidColorBrush(_theme.Ink);
        _englishButton.Margin = new Thickness(0, 0, 0, 10);
        _englishButton.IsChecked = _settings.Language == "en";
        _englishButton.Checked += (_, _) => _settings.Language = "en";

        _russianButton.Content = "Русский";
        _russianButton.Foreground = new SolidColorBrush(_theme.Ink);
        _russianButton.IsChecked = _settings.Language == "ru";
        _russianButton.Checked += (_, _) => _settings.Language = "ru";

        _germanButton.Content = "Deutsch";
        _germanButton.Foreground = new SolidColorBrush(_theme.Ink);
        _germanButton.Margin = new Thickness(0, 10, 0, 0);
        _germanButton.IsChecked = _settings.Language == "de";
        _germanButton.Checked += (_, _) => _settings.Language = "de";

        return BuildTabBody(
            T("Language", "Язык", "Sprache"),
            T(
                "Choose the interface language. The app can switch immediately after saving.",
                "Выбери язык интерфейса. После сохранения приложение сразу переключится.",
                "Wähle die Sprache der Oberfläche. Nach dem Speichern wechselt die App sofort."),
            new StackPanel
            {
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    _englishButton,
                    _russianButton,
                    _germanButton
                }
            });
    }

    private UIElement BuildThemesTab()
    {
        var list = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        foreach (var option in _themes)
        {
            var button = new RadioButton
            {
                Tag = option.Id,
                IsChecked = option.Id == _settings.Theme,
                Foreground = new SolidColorBrush(_theme.Ink),
                Margin = new Thickness(0, 0, 0, 12),
                Content = BuildThemeRow(option)
            };
            button.Checked += (_, _) => _settings.Theme = option.Id;
            list.Children.Add(button);
        }

        return BuildTabBody(
            T("Themes", "Темы", "Themen"),
            T(
                "Pick the whole app mood. Themes affect the sidebar, panels, buttons, and editor surfaces.",
                "Выбери настроение приложения. Тема меняет боковую панель, блоки, кнопки и редактор.",
                "Wähle die Stimmung der App. Themen ändern Seitenleiste, Flächen, Buttons und Editor."),
            list);
    }

    private UIElement BuildEditorTab()
    {
        _markerBox.MinWidth = 220;
        _markerBox.Margin = new Thickness(0, 8, 0, 0);
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Purple marker", "Фиолетовый маркер", "Violetter Marker"), Tag = "#D8B4FE" });
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Blue marker", "Синий маркер", "Blauer Marker"), Tag = "#BFDBFE" });
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Green marker", "Зелёный маркер", "Grüner Marker"), Tag = "#BBF7D0" });
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Yellow marker", "Жёлтый маркер", "Gelber Marker"), Tag = "#FEF08A" });
        _markerBox.Items.Add(new ComboBoxItem { Content = T("Gray marker", "Серый маркер", "Grauer Marker"), Tag = "#E5E7EB" });
        foreach (ComboBoxItem item in _markerBox.Items)
        {
            if (Equals(item.Tag, _settings.DefaultMarkerColor))
            {
                _markerBox.SelectedItem = item;
                break;
            }
        }

        _markerBox.SelectedItem ??= _markerBox.Items[0];

        _markerBox.SelectionChanged += (_, _) =>
        {
            if (_markerBox.SelectedItem is ComboBoxItem { Tag: string color })
            {
                _settings.DefaultMarkerColor = color;
            }
        };

        return BuildTabBody(
            T("Editor", "Редактор", "Editor"),
            T(
                "Small defaults for writing and marking text.",
                "Небольшие настройки письма и маркера.",
                "Kleine Standards für Schreiben und Markieren."),
            new StackPanel
            {
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new TextBlock
                    {
                        Text = T("Default marker color", "Цвет маркера по умолчанию", "Standardfarbe des Markers"),
                        Foreground = new SolidColorBrush(_theme.Muted),
                        FontWeight = FontWeights.SemiBold
                    },
                    _markerBox
                }
            });
    }

    private UIElement BuildStorageTab(string storagePath, string? activeWorkspacePath)
    {
        return BuildTabBody(
            T("Storage", "Хранилище", "Speicher"),
            T(
                "A quick look at where the app keeps its library and the current workspace files.",
                "Быстрый взгляд на то, где приложение хранит библиотеку и файлы текущего воркспейса.",
                "Ein kurzer Blick darauf, wo die App Bibliothek und aktuelle Arbeitsbereich-Dateien speichert."),
            new StackPanel
            {
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    BuildPathBlock(T("Library JSON", "JSON библиотеки", "Bibliothek-JSON"), storagePath),
                    BuildPathBlock(
                        T("Active workspace folder", "Папка текущего воркспейса", "Ordner des aktiven Arbeitsbereichs"),
                        activeWorkspacePath ?? T("No workspace selected", "Воркспейс не выбран", "Kein Arbeitsbereich ausgewählt"))
                }
            });
    }

    private UIElement BuildTabBody(string title, string subtitle, UIElement content)
    {
        return new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    Foreground = new SolidColorBrush(_theme.Ink),
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = subtitle,
                    Foreground = new SolidColorBrush(_theme.Muted),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 0)
                },
                content
            }
        };
    }

    private UIElement BuildThemeRow(AppThemeOption option)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var swatches = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 1, 12, 0)
        };
        foreach (var color in new[] { option.Side, option.Panel, option.Accent, option.Warm })
        {
            swatches.Children.Add(new Border
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 0, 4, 0),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(_theme.Line),
                BorderThickness = new Thickness(1)
            });
        }

        row.Children.Add(swatches);
        var label = new TextBlock
        {
            Text = _settings.Language switch
            {
                "ru" => option.RussianName,
                "de" => option.GermanName,
                _ => option.EnglishName
            },
            Foreground = new SolidColorBrush(_theme.Ink),
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        return row;
    }

    private UIElement BuildPathBlock(string label, string value)
    {
        return new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 14),
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Foreground = new SolidColorBrush(_theme.Muted),
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = value,
                    Foreground = new SolidColorBrush(_theme.Ink),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                }
            }
        };
    }

    private UIElement BuildActions()
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16)
        };

        actions.Children.Add(CreateButton(T("Cancel", "Отмена", "Abbrechen"), new SolidColorBrush(_theme.Ghost), new SolidColorBrush(_theme.Ink), (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton(T("Save", "Сохранить", "Speichern"), new SolidColorBrush(_theme.Accent), ThemeContrast.ForegroundOn(_theme.Accent, _theme), (_, _) => DialogResult = true, new Thickness(10, 0, 0, 0)));
        return actions;
    }

    private static Button CreateButton(
        string text,
        Brush background,
        Brush foreground,
        RoutedEventHandler onClick,
        Thickness? margin = null)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 36,
            MinWidth = 96,
            Padding = new Thickness(14, 6, 14, 6),
            BorderThickness = new Thickness(0),
            Background = background,
            Foreground = foreground,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Margin = margin ?? new Thickness(0)
        };
        button.Click += onClick;
        return button;
    }

    private string T(string english, string russian)
    {
        return T(english, russian, english);
    }

    private string T(string english, string russian, string german)
    {
        return _settings.Language switch
        {
            "ru" => russian,
            "de" => german,
            _ => english
        };
    }
}

