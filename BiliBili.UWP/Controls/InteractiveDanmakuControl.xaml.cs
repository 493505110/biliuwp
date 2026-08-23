using BiliBili.UWP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace BiliBili.UWP.Controls
{
    public sealed partial class InteractiveDanmakuControl : UserControl
    {
        private InteractiveDanmakuModel currentItem;
        private readonly List<VoteOptionVisual> voteOptionVisuals = new List<VoteOptionVisual>();
        private readonly List<GradeOptionVisual> gradeOptionVisuals = new List<GradeOptionVisual>();
        private bool resultVisible;

        public InteractiveDanmakuControl()
        {
            InitializeComponent();
        }

        public event EventHandler<InteractiveDanmakuActionEventArgs> ActionRequested;

        public InteractiveDanmakuModel CurrentItem
        {
            get { return currentItem; }
        }

        public void ShowItem(InteractiveDanmakuModel item)
        {
            currentItem = item;
            resultVisible = false;
            voteOptionVisuals.Clear();
            gradeOptionVisuals.Clear();
            optionsPanel.Children.Clear();
            optionsPanel.RowDefinitions.Clear();
            statusText.Text = string.Empty;
            statusText.Visibility = Visibility.Collapsed;

            if (item == null)
            {
                HideItem();
                return;
            }

            titleText.Text = item.Title;
            summaryText.Text = item.Type == InteractiveDanmakuType.Vote
                ? "请选择一个选项"
                : BuildGradeSummary(item);

            if (item.Type == InteractiveDanmakuType.Vote)
            {
                foreach (var option in item.Options)
                {
                    var button = new InteractiveDanmakuOptionButton
                    {
                        Tag = option.Index,
                        Style = (Style)Resources["InteractiveDanmakuOptionButtonStyle"],
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Margin = new Thickness(0, 3, 0, 0),
                        Padding = new Thickness(10, 6, 10, 6)
                    };
                    TextBlock percentageText;
                    button.Content = CreateVoteOptionContent(option.Text, out percentageText);
                    button.Click += OptionButton_Click;
                    optionsPanel.RowDefinitions.Add(new RowDefinition
                    {
                        Height = GridLength.Auto
                    });
                    Grid.SetRow(button, optionsPanel.RowDefinitions.Count - 1);
                    optionsPanel.Children.Add(button);
                    voteOptionVisuals.Add(new VoteOptionVisual(option, button, percentageText));
                }
            }
            else
            {
                var gradePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                for (var score = 1; score <= 5; score++)
                {
                    var button = new InteractiveDanmakuOptionButton
                    {
                        Content = score + " 分",
                        Tag = score,
                        Style = (Style)Resources["InteractiveDanmakuOptionButtonStyle"],
                        Margin = new Thickness(3, 0, 3, 0),
                        Padding = new Thickness(10, 6, 10, 6)
                    };
                    button.Click += OptionButton_Click;
                    gradePanel.Children.Add(button);
                    gradeOptionVisuals.Add(new GradeOptionVisual(score, button));
                }
                optionsPanel.RowDefinitions.Add(new RowDefinition
                {
                    Height = GridLength.Auto
                });
                optionsPanel.Children.Add(gradePanel);
            }

            Visibility = Visibility.Visible;
            if (item.Type == InteractiveDanmakuType.Vote && item.VoteSubmitted)
            {
                ShowVoteResult(item.SelectedVoteOption);
            }
            else if (item.Type == InteractiveDanmakuType.Grade && item.GradeSubmitted)
            {
                ShowGradeResult(item.SelectedGradeScore);
            }
        }

        public void HideItem()
        {
            currentItem = null;
            resultVisible = false;
            voteOptionVisuals.Clear();
            gradeOptionVisuals.Clear();
            optionsPanel.Children.Clear();
            optionsPanel.RowDefinitions.Clear();
            statusText.Text = string.Empty;
            statusText.Visibility = Visibility.Collapsed;
            Visibility = Visibility.Collapsed;
        }

        public void SetActionEnabled(bool enabled)
        {
            foreach (var child in optionsPanel.Children)
            {
                var button = child as Button;
                if (button != null)
                {
                    button.IsEnabled = enabled;
                    continue;
                }

                var panel = child as Panel;
                if (panel == null)
                {
                    continue;
                }

                foreach (var nestedChild in panel.Children)
                {
                    var nestedButton = nestedChild as Button;
                    if (nestedButton != null)
                    {
                        nestedButton.IsEnabled = enabled;
                    }
                }
            }
        }

        public void ShowStatus(string message)
        {
            statusText.Text = message ?? string.Empty;
            statusText.Visibility = string.IsNullOrWhiteSpace(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public void ShowVoteResult(int selectedOption)
        {
            if (currentItem == null
                || currentItem.Type != InteractiveDanmakuType.Vote
                || resultVisible)
            {
                return;
            }

            if (!currentItem.VoteSubmitted)
            {
                var selected = currentItem.Options.FirstOrDefault(option => option.Index == selectedOption);
                if (selected != null)
                {
                    selected.Count = Math.Max(0, selected.Count) + 1;
                }

                currentItem.SelectedVoteOption = selectedOption;
                currentItem.VoteSubmitted = true;
            }
            else
            {
                selectedOption = currentItem.SelectedVoteOption;
            }

            var total = currentItem.Options.Sum(option => Math.Max(0, option.Count));
            summaryText.Text = "投票结果";
            if (string.IsNullOrWhiteSpace(statusText.Text))
            {
                ShowStatus("已提交");
            }
            resultVisible = true;
            SetActionEnabled(true);

            foreach (var visual in voteOptionVisuals)
            {
                var count = Math.Max(0, visual.Option.Count);
                var percentage = total <= 0 ? 0 : count * 100d / total;
                ApplySelectionVisual(
                    visual.Button,
                    visual.Option.Index == selectedOption,
                    true);
                visual.ShowPercentage(percentage);
            }
        }

        public void ShowGradeResult(int selectedScore)
        {
            if (currentItem == null
                || currentItem.Type != InteractiveDanmakuType.Grade
                || resultVisible)
            {
                return;
            }

            selectedScore = Math.Max(1, Math.Min(5, selectedScore));
            if (!currentItem.GradeSubmitted)
            {
                var previousCount = Math.Max(0, currentItem.Count);
                var previousAverageScore = Math.Max(0, currentItem.AverageScore);
                var submittedScore = selectedScore * 2;

                currentItem.Count = previousCount + 1;
                currentItem.AverageScore = previousCount <= 0
                    ? submittedScore
                    : (previousAverageScore * previousCount + submittedScore) / currentItem.Count;
                currentItem.SelectedGradeScore = selectedScore;
                currentItem.GradeSubmitted = true;
            }
            else
            {
                selectedScore = currentItem.SelectedGradeScore;
            }

            summaryText.Text = string.Format(
                "已评分 {0} 分，当前平均 {1:0.0} 分，共 {2} 人参与",
                selectedScore,
                currentItem.AverageScore / 2,
                Math.Max(0, currentItem.Count));
            resultVisible = true;
            SetActionEnabled(true);

            foreach (var visual in gradeOptionVisuals)
            {
                ApplySelectionVisual(visual.Button, visual.Score == selectedScore);
            }
        }

        private void ApplySelectionVisual(Button button, bool selected, bool voteResult = false)
        {
            if (button == null)
            {
                return;
            }

            button.IsHitTestVisible = false;
            if (voteResult)
            {
                button.Style = (Style)Resources[selected
                    ? "InteractiveDanmakuSelectedResultOptionButtonStyle"
                    : "InteractiveDanmakuResultOptionButtonStyle"];
                button.Opacity = selected ? 1 : 0.65;
                return;
            }

            if (selected)
            {
                button.Style = (Style)Resources["InteractiveDanmakuSelectedOptionButtonStyle"];
                button.Opacity = 1;
            }
            else
            {
                button.Opacity = 0.65;
            }
        }

        private void OptionButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null || currentItem == null || resultVisible)
            {
                return;
            }

            var value = button.Tag is int ? (int)button.Tag : 0;
            ActionRequested?.Invoke(this, new InteractiveDanmakuActionEventArgs(currentItem, value));
        }

        private static Grid CreateVoteOptionContent(string text, out TextBlock percentageText)
        {
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            var optionText = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            percentageText = new TextBlock
            {
                Margin = new Thickness(12, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(percentageText, 1);
            content.Children.Add(optionText);
            content.Children.Add(percentageText);
            return content;
        }

        private static string BuildGradeSummary(InteractiveDanmakuModel item)
        {
            if (item.Count <= 0 || item.AverageScore <= 0)
            {
                return "请选择 1 到 5 分";
            }

            return string.Format("当前平均 {0:0.0} 分，共 {1} 人参与", item.AverageScore / 2, item.Count);
        }

        private sealed class InteractiveDanmakuOptionButton : Button
        {
            protected override void OnKeyDown(KeyRoutedEventArgs e)
            {
                if (e.Key == Windows.System.VirtualKey.Space)
                {
                    e.Handled = true;
                    return;
                }

                base.OnKeyDown(e);
            }
        }

        private sealed class VoteOptionVisual
        {
            public VoteOptionVisual(
                InteractiveDanmakuOption option,
                Button button,
                TextBlock percentageText)
            {
                Option = option;
                Button = button;
                PercentageText = percentageText;
            }

            public InteractiveDanmakuOption Option { get; }
            public Button Button { get; }
            public TextBlock PercentageText { get; }

            private double percentage;

            public void ShowPercentage(double percentage)
            {
                this.percentage = Math.Max(0, Math.Min(100, percentage));
                PercentageText.Text = string.Format("{0:0.#}%", this.percentage);
                PercentageText.Visibility = Visibility.Visible;
                Button.Background = CreateResultBackground(this.percentage);
            }

            private static Brush CreateResultBackground(double percentage)
            {
                var fillBrush = Application.Current.Resources["Bili-Color"] as SolidColorBrush;
                var trackBrush = Application.Current.Resources["Bili-ForeColor-Dark"] as SolidColorBrush;
                if (fillBrush == null || trackBrush == null)
                {
                    return null;
                }

                var fillColor = fillBrush.Color;
                var trackColor = Darken(trackBrush.Color, 0.28);
                var offset = Math.Max(0, Math.Min(1, percentage / 100));
                var background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5)
                };

                if (offset <= 0)
                {
                    background.GradientStops.Add(new GradientStop
                    {
                        Color = trackColor,
                        Offset = 0
                    });
                    background.GradientStops.Add(new GradientStop
                    {
                        Color = trackColor,
                        Offset = 1
                    });
                }
                else if (offset >= 1)
                {
                    background.GradientStops.Add(new GradientStop
                    {
                        Color = fillColor,
                        Offset = 0
                    });
                    background.GradientStops.Add(new GradientStop
                    {
                        Color = fillColor,
                        Offset = 1
                    });
                }
                else
                {
                    background.GradientStops.Add(new GradientStop
                    {
                        Color = fillColor,
                        Offset = 0
                    });
                    background.GradientStops.Add(new GradientStop
                    {
                        Color = fillColor,
                        Offset = offset
                    });
                    background.GradientStops.Add(new GradientStop
                    {
                        Color = trackColor,
                        Offset = offset
                    });
                    background.GradientStops.Add(new GradientStop
                    {
                        Color = trackColor,
                        Offset = 1
                    });
                }

                return background;
            }

            private static Color Darken(Color color, double amount)
            {
                var factor = 1 - Math.Max(0, Math.Min(1, amount));
                return Color.FromArgb(
                    color.A,
                    (byte)Math.Round(color.R * factor),
                    (byte)Math.Round(color.G * factor),
                    (byte)Math.Round(color.B * factor));
            }
        }

        private sealed class GradeOptionVisual
        {
            public GradeOptionVisual(int score, Button button)
            {
                Score = score;
                Button = button;
            }

            public int Score { get; }
            public Button Button { get; }
        }
    }

    public sealed class InteractiveDanmakuActionEventArgs : EventArgs
    {
        public InteractiveDanmakuActionEventArgs(InteractiveDanmakuModel item, int value)
        {
            Item = item;
            Value = value;
        }

        public InteractiveDanmakuModel Item { get; }
        public int Value { get; }
    }
}
