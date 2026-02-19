using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Fap.Presentation.Controls
{
    public partial class BreadcrumbBar : UserControl
    {
        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register(
                nameof(Path),
                typeof(string),
                typeof(BreadcrumbBar),
                new FrameworkPropertyMetadata(string.Empty, OnPathPropertyChanged));

        public static readonly DependencyProperty IsProgressActiveProperty =
            DependencyProperty.Register(
                nameof(IsProgressActive),
                typeof(bool),
                typeof(BreadcrumbBar),
                new FrameworkPropertyMetadata(false, OnIsProgressActiveChanged));

        public static readonly RoutedEvent PathChangedEvent =
            EventManager.RegisterRoutedEvent(
                "PathChanged",
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<string>),
                typeof(BreadcrumbBar));

        private bool _suppressPathChangedEvent;

        public BreadcrumbBar()
        {
            InitializeComponent();
        }

        public string Path
        {
            get => (string)GetValue(PathProperty);
            set => SetValue(PathProperty, value);
        }

        public bool IsProgressActive
        {
            get => (bool)GetValue(IsProgressActiveProperty);
            set => SetValue(IsProgressActiveProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<string> PathChanged
        {
            add => AddHandler(PathChangedEvent, value);
            remove => RemoveHandler(PathChangedEvent, value);
        }

        private static void OnPathPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BreadcrumbBar bar)
                bar.RebuildSegments();
        }

        private static void OnIsProgressActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BreadcrumbBar bar)
            {
                bar.ProgressIndicator.Visibility = (bool)e.NewValue
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if ((bool)e.NewValue)
                {
                    bar.ProgressIndicator.IsIndeterminate = true;
                }
                else
                {
                    bar.ProgressIndicator.IsIndeterminate = false;
                    bar.ProgressIndicator.Value = 0;
                }
            }
        }

        private void RebuildSegments()
        {
            SegmentsPanel.Children.Clear();

            var path = Path;
            if (string.IsNullOrEmpty(path))
                return;

            var segments = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return;

            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                {
                    var separator = new TextBlock();
                    separator.SetResourceReference(StyleProperty, "SeparatorStyle");
                    SegmentsPanel.Children.Add(separator);
                }

                var segmentIndex = i;
                var button = new Button
                {
                    Content = segments[i],
                    Tag = string.Join("\\", segments.Take(segmentIndex + 1))
                };
                button.SetResourceReference(StyleProperty, "SegmentButtonStyle");
                button.Click += SegmentButton_Click;
                SegmentsPanel.Children.Add(button);
            }
        }

        private void SegmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string newPath)
            {
                var oldPath = Path;
                if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                    return;

                _suppressPathChangedEvent = true;
                Path = newPath;
                _suppressPathChangedEvent = false;

                RaiseEvent(new RoutedPropertyChangedEventArgs<string>(
                    oldPath, newPath, PathChangedEvent));
            }
        }
    }
}
