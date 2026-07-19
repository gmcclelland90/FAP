using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fap.Client.WinUI.Views;

/// <summary>
/// Shared PowerToys/Fluent chrome helpers for feature views (section cards, page headers).
/// </summary>
internal static class FluentChrome
{
    public static StackPanel PageHeader(string title, string? subtitle)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            panel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 13,
                Opacity = 0.75,
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }
        return panel;
    }

    public static Border Section(string title, string? description, params UIElement[] children)
    {
        var header = new StackPanel { Spacing = 2, Margin = new Thickness(16, 14, 16, 4) };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        if (!string.IsNullOrWhiteSpace(description))
        {
            header.Children.Add(SecondaryCaption(description!));
        }

        var body = new StackPanel { Spacing = 12, Margin = new Thickness(16, 8, 16, 16) };
        foreach (var child in children)
            body.Children.Add(child);

        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(body);
        return Card(root);
    }

    public static Border Card(UIElement child, Thickness? margin = null)
    {
        var card = new Border
        {
            Child = child,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Margin = margin ?? new Thickness(0)
        };
        if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var bg) && bg is Brush bgBrush)
            card.Background = bgBrush;
        if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out var stroke) && stroke is Brush strokeBrush)
            card.BorderBrush = strokeBrush;
        return card;
    }

    public static TextBlock SecondaryCaption(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Opacity = 0.75,
        TextWrapping = TextWrapping.WrapWholeWords
    };

    public static StackPanel EmptyState(string glyph, string title, string body)
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
            Padding = new Thickness(24),
            MaxWidth = 360
        };
        panel.Children.Add(new FontIcon { Glyph = glyph, FontSize = 32, Opacity = 0.55 });
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = body,
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        return panel;
    }

    public static void TryApplyStyle(FrameworkElement element, string resourceKey)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out var style) && style is Style s)
            element.Style = s;
    }
}
