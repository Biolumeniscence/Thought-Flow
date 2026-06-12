using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ThoughtFlow;

public sealed class TextPromptDialog : Window
{
    private readonly TextBox _input = new();
    private readonly string _language;
    private readonly AppThemeOption _theme;

    private TextPromptDialog(string title, string label, string currentValue, AppThemeOption theme, string language)
    {
        _theme = theme;
        _language = language;
        Title = title;
        Width = 430;
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
        Loaded += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };

        Content = new Border
        {
            Background = ThemeContrast.Brush(theme.Panel),
            BorderBrush = ThemeContrast.Brush(theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent(title, label, currentValue)
        };
    }

    public static string? Prompt(Window owner, string title, string label, string currentValue, AppThemeOption theme, string language)
    {
        var dialog = new TextPromptDialog(title, label, currentValue, theme, language)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog._input.Text : null;
    }

    private UIElement BuildContent(string title, string label, string currentValue)
    {
        var root = new DockPanel();
        var header = new Border
        {
            Background = ThemeContrast.Brush(_theme.Side),
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(18, 15, 18, 14),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = ThemeContrast.ForegroundOn(_theme.Side, _theme),
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = T("Give it a clear name for the sidebar.", "Дай понятное название для боковой панели.", "Gib einen klaren Namen für die Seitenleiste ein."),
                        Foreground = ThemeContrast.Brush(_theme.SideMuted),
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                }
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        _input.Text = currentValue;
        _input.MinHeight = 38;
        _input.Margin = new Thickness(0, 7, 0, 18);
        _input.Padding = new Thickness(10);
        _input.BorderBrush = ThemeContrast.Brush(_theme.Line);
        _input.BorderThickness = new Thickness(1);
        _input.Foreground = ThemeContrast.Brush(_theme.Ink);
        _input.Background = ThemeContrast.Brush(_theme.Input);
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
            }
        };

        root.Children.Add(new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Foreground = ThemeContrast.Brush(_theme.Muted),
                    FontWeight = FontWeights.SemiBold
                },
                _input,
                BuildActions()
            }
        });

        return root;
    }

    private UIElement BuildActions()
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        actions.Children.Add(CreateButton(
            T("Cancel", "Отмена", "Abbrechen"),
            ThemeContrast.Brush(_theme.Ghost),
            ThemeContrast.ForegroundOn(_theme.Ghost, _theme),
            (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton(
            T("Save", "Сохранить", "Speichern"),
            ThemeContrast.Brush(_theme.Accent),
            ThemeContrast.ForegroundOn(_theme.Accent, _theme),
            (_, _) => DialogResult = true,
            new Thickness(10, 0, 0, 0)));
        return actions;
    }

    private string T(string english, string russian)
    {
        return T(english, russian, english);
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
}

