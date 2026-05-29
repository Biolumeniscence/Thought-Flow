using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ThoughtFlow;

public sealed class TextPromptDialog : Window
{
    private static readonly SolidColorBrush InkBrush = new(Color.FromRgb(35, 35, 35));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(108, 104, 97));
    private static readonly SolidColorBrush PanelBrush = new(Color.FromRgb(255, 252, 247));
    private static readonly SolidColorBrush LineBrush = new(Color.FromRgb(216, 208, 196));
    private static readonly SolidColorBrush SideBrush = new(Color.FromRgb(37, 49, 55));
    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(49, 91, 122));
    private static readonly SolidColorBrush GhostBrush = new(Color.FromRgb(233, 226, 216));

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
            Background = new SolidColorBrush(theme.Panel),
            BorderBrush = new SolidColorBrush(theme.Line),
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
            Background = SideBrush,
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(18, 15, 18, 14),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = Brushes.White,
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = T("Give it a clear name for the sidebar.", "Дай понятное название для боковой панели.", "Gib einen klaren Namen für die Seitenleiste ein."),
                        Foreground = new SolidColorBrush(Color.FromRgb(184, 196, 201)),
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
        _input.BorderBrush = LineBrush;
        _input.BorderThickness = new Thickness(1);
        _input.Foreground = new SolidColorBrush(_theme.Ink);
        _input.Background = new SolidColorBrush(_theme.Input);
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
                    Foreground = MutedBrush,
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

        actions.Children.Add(CreateButton(T("Cancel", "Отмена", "Abbrechen"), GhostBrush, InkBrush, (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton(T("Save", "Сохранить", "Speichern"), AccentBrush, Brushes.White, (_, _) => DialogResult = true, new Thickness(10, 0, 0, 0)));
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

