using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ThoughtFlow;

public sealed class ImportTextDialog : Window
{
    private const int MinWords = 1;
    private const int MaxWords = 700;
    private const int DefaultWords = 500;

    private readonly AppThemeOption _theme;
    private readonly string _language;
    private readonly Slider _wordSlider = new();
    private readonly Button _wordValueButton = new();
    private readonly TextBox _wordValueInput = new();
    private readonly CheckBox _paragraphCheck = new();
    private bool _isUpdatingWordValue;

    private ImportTextDialog(AppThemeOption theme, string language)
    {
        _theme = theme;
        _language = language;
        Title = T("Import text", "Импорт текста", "Text importieren");
        Width = 480;
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
        Loaded += (_, _) => _wordSlider.Focus();

        Content = new Border
        {
            Background = Brush(theme.Panel),
            BorderBrush = Brush(theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = BuildContent()
        };
    }

    public TextImportOptions Options { get; private set; } = new(TextImportSplitMode.Words, DefaultWords);

    public static TextImportOptions? Prompt(Window owner, AppThemeOption theme, string language)
    {
        var dialog = new ImportTextDialog(theme, language)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.Options : null;
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel();
        var header = new Border
        {
            Background = Brush(_theme.Side),
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Padding = new Thickness(18, 15, 18, 14),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = T("Split imported text", "Разбить импортируемый текст", "Importierten Text aufteilen"),
                        Foreground = Brush(_theme.SideText),
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = T(
                            "Choose how the document becomes messages.",
                            "Выбери, как документ превратится в сообщения.",
                            "Wähle, wie das Dokument in Nachrichten aufgeteilt wird."),
                        Foreground = Brush(_theme.SideMuted),
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                }
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        _paragraphCheck.Content = T("Split every paragraph into a separate message", "Разбить каждый абзац в отдельное сообщение", "Jeden Absatz als eigene Nachricht importieren");
        _paragraphCheck.Foreground = Brush(_theme.Ink);
        _paragraphCheck.Margin = new Thickness(0, 16, 0, 0);
        _paragraphCheck.Checked += (_, _) => UpdateWordInputState();
        _paragraphCheck.Unchecked += (_, _) => UpdateWordInputState();

        var body = new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = T("Words per message", "Слов в одном сообщении", "Wörter pro Nachricht"),
                    Foreground = Brush(_theme.Muted),
                    FontWeight = FontWeights.SemiBold
                },
                BuildWordSelector(),
                _paragraphCheck,
                BuildActions()
            }
        };

        root.Children.Add(body);
        SetWordValue(DefaultWords);
        return root;
    }

    private UIElement BuildWordSelector()
    {
        _wordValueButton.MinWidth = 78;
        _wordValueButton.MinHeight = 34;
        _wordValueButton.Padding = new Thickness(12, 5, 12, 5);
        _wordValueButton.BorderThickness = new Thickness(1);
        _wordValueButton.BorderBrush = Brush(_theme.Line);
        _wordValueButton.Background = Brush(Mix(_theme.Panel, _theme.AccentSoft, 0.72));
        _wordValueButton.Foreground = Brush(_theme.Ink);
        _wordValueButton.FontWeight = FontWeights.SemiBold;
        _wordValueButton.Cursor = Cursors.Hand;
        _wordValueButton.Click += (_, _) => BeginManualWordEdit();

        _wordValueInput.Width = 88;
        _wordValueInput.MinHeight = 34;
        _wordValueInput.Padding = new Thickness(10, 4, 10, 4);
        _wordValueInput.BorderBrush = Brush(_theme.Line);
        _wordValueInput.BorderThickness = new Thickness(1);
        _wordValueInput.Background = Brush(_theme.Input);
        _wordValueInput.Foreground = Brush(_theme.Ink);
        _wordValueInput.TextAlignment = TextAlignment.Center;
        _wordValueInput.Visibility = Visibility.Collapsed;
        _wordValueInput.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        _wordValueInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitManualWordEdit();
                e.Handled = true;
            }

            if (e.Key == Key.Escape)
            {
                CancelManualWordEdit();
                e.Handled = true;
            }
        };
        _wordValueInput.LostKeyboardFocus += (_, _) => CommitManualWordEdit();

        _wordSlider.Minimum = MinWords;
        _wordSlider.Maximum = MaxWords;
        _wordSlider.Value = DefaultWords;
        _wordSlider.TickFrequency = 1;
        _wordSlider.IsSnapToTickEnabled = true;
        _wordSlider.Background = Brush(Mix(_theme.Panel, _theme.Ghost, 0.75));
        _wordSlider.Foreground = Brush(_theme.Accent);
        _wordSlider.Margin = new Thickness(0, 12, 0, 0);
        _wordSlider.ValueChanged += (_, _) =>
        {
            if (_isUpdatingWordValue)
            {
                return;
            }

            SetWordValue((int)Math.Round(_wordSlider.Value));
        };

        var valueHost = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        valueHost.Children.Add(_wordValueButton);
        valueHost.Children.Add(_wordValueInput);

        var rangeLabels = new Grid
        {
            Margin = new Thickness(0, 4, 0, 0)
        };
        rangeLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rangeLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var minLabel = new TextBlock
        {
            Text = MinWords.ToString(),
            Foreground = Brush(_theme.Muted),
            FontSize = 11
        };
        var maxLabel = new TextBlock
        {
            Text = MaxWords.ToString(),
            Foreground = Brush(_theme.Muted),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(maxLabel, 1);
        rangeLabels.Children.Add(minLabel);
        rangeLabels.Children.Add(maxLabel);

        return new Border
        {
            Background = Brush(Mix(_theme.Panel, _theme.Input, 0.62)),
            BorderBrush = Brush(_theme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 12, 12, 10),
            Margin = new Thickness(0, 7, 0, 0),
            Child = new StackPanel
            {
                Children =
                {
                    valueHost,
                    _wordSlider,
                    rangeLabels
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
            Margin = new Thickness(0, 18, 0, 0)
        };

        actions.Children.Add(CreateButton(T("Cancel", "Отмена", "Abbrechen"), Brush(_theme.Ghost), Brush(_theme.Ink), (_, _) => DialogResult = false));
        actions.Children.Add(CreateButton(T("Import", "Импорт", "Importieren"), Brush(_theme.Accent), ThemeContrast.ForegroundOn(_theme.Accent, _theme), (_, _) => Accept(), new Thickness(10, 0, 0, 0)));
        return actions;
    }

    private void Accept()
    {
        if (_paragraphCheck.IsChecked == true)
        {
            Options = new TextImportOptions(TextImportSplitMode.Paragraphs, GetWordValue());
            DialogResult = true;
            return;
        }

        Options = new TextImportOptions(TextImportSplitMode.Words, GetWordValue());
        DialogResult = true;
    }

    private int GetWordValue()
    {
        return (int)Math.Round(_wordSlider.Value);
    }

    private void SetWordValue(int value)
    {
        var normalized = Math.Clamp(value, MinWords, MaxWords);
        _isUpdatingWordValue = true;
        _wordSlider.Value = normalized;
        _wordValueButton.Content = normalized.ToString();
        _wordValueInput.Text = normalized.ToString();
        _isUpdatingWordValue = false;
    }

    private void BeginManualWordEdit()
    {
        _wordValueInput.Text = GetWordValue().ToString();
        _wordValueButton.Visibility = Visibility.Collapsed;
        _wordValueInput.Visibility = Visibility.Visible;
        _wordValueInput.Focus();
        _wordValueInput.SelectAll();
    }

    private void CommitManualWordEdit()
    {
        if (_wordValueInput.Visibility != Visibility.Visible)
        {
            return;
        }

        if (int.TryParse(_wordValueInput.Text, out var value))
        {
            SetWordValue(value);
        }
        else
        {
            SetWordValue(GetWordValue());
        }

        _wordValueInput.Visibility = Visibility.Collapsed;
        _wordValueButton.Visibility = Visibility.Visible;
    }

    private void CancelManualWordEdit()
    {
        _wordValueInput.Visibility = Visibility.Collapsed;
        _wordValueButton.Visibility = Visibility.Visible;
        SetWordValue(GetWordValue());
    }

    private void UpdateWordInputState()
    {
        var enabled = _paragraphCheck.IsChecked != true;
        _wordSlider.IsEnabled = enabled;
        _wordValueButton.IsEnabled = enabled;
        _wordValueInput.IsEnabled = enabled;
        _wordSlider.Opacity = enabled ? 1 : 0.45;
        _wordValueButton.Opacity = enabled ? 1 : 0.45;
        _wordValueInput.Opacity = enabled ? 1 : 0.45;
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
            MinHeight = 34,
            MinWidth = 90,
            Padding = new Thickness(12, 6, 12, 6),
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

    private static SolidColorBrush Brush(Color color)
    {
        return new SolidColorBrush(color);
    }

    private static Color Mix(Color left, Color right, double rightAmount)
    {
        var clamped = Math.Clamp(rightAmount, 0, 1);
        var leftAmount = 1 - clamped;
        return Color.FromRgb(
            (byte)Math.Round(left.R * leftAmount + right.R * clamped),
            (byte)Math.Round(left.G * leftAmount + right.G * clamped),
            (byte)Math.Round(left.B * leftAmount + right.B * clamped));
    }
}
