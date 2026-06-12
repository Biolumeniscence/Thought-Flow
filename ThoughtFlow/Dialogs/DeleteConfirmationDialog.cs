using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ThoughtFlow;

public sealed class DeleteConfirmationDialog : Window
{
    private readonly AppThemeOption _theme;
    private readonly string _language;

    private DeleteConfirmationDialog(string itemType, string itemName, string detail, AppThemeOption theme, string language)
    {
        _theme = theme;
        _language = language;
        Title = T($"Delete {itemType}", $"Удалить {itemType}", $"{itemType} löschen");
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

        Content = new Border
        {
            Background = ThemeContrast.Brush(theme.Panel),
            BorderBrush = ThemeContrast.Brush(theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent(itemType, itemName, detail)
        };
    }

    public static bool Confirm(Window owner, string itemType, string itemName, string detail, AppThemeOption theme, string language)
    {
        var dialog = new DeleteConfirmationDialog(itemType, itemName, detail, theme, language)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true;
    }

    private UIElement BuildContent(string itemType, string itemName, string detail)
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
                        Text = T($"Delete {itemType}?", $"Удалить {itemType}?", $"{itemType} löschen?"),
                        Foreground = ThemeContrast.ForegroundOn(_theme.Side, _theme),
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = T("This action needs confirmation.", "Это действие нужно подтвердить.", "Diese Aktion muss bestätigt werden."),
                        Foreground = ThemeContrast.Brush(_theme.SideMuted),
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
                new TextBlock
                {
                    Text = T(
                        $"Are you sure you want to delete {itemType} \"{itemName}\"?",
                        $"Точно удалить {itemType} \"{itemName}\"?",
                        $"{itemType} \"{itemName}\" wirklich löschen?"),
                    Foreground = ThemeContrast.Brush(_theme.Ink),
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = detail,
                    Foreground = ThemeContrast.Brush(_theme.Muted),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 18)
                },
                BuildActions()
            }
        };

        root.Children.Add(body);
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
            T("Delete", "Удалить", "Löschen"),
            ThemeContrast.Brush(_theme.Warm),
            ThemeContrast.ForegroundOn(_theme.Warm, _theme),
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

