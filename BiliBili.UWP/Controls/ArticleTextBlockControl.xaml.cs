using BiliBili.UWP.Models;
using System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace BiliBili.UWP.Controls
{
    public sealed partial class ArticleTextBlockControl : UserControl
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
            "Model",
            typeof(ArticleTextBlockModel),
            typeof(ArticleTextBlockControl),
            new PropertyMetadata(null, OnModelChanged));

        public ArticleTextBlockControl()
        {
            InitializeComponent();
        }

        public event EventHandler<string> LinkClicked;

        public ArticleTextBlockModel Model
        {
            get { return (ArticleTextBlockModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        private static void OnModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            ((ArticleTextBlockControl)sender).Render(args.NewValue as ArticleTextBlockModel);
        }

        private void Render(ArticleTextBlockModel model)
        {
            richText.Blocks.Clear();
            if (model == null)
            {
                return;
            }

            Paragraph paragraph = new Paragraph();
            ApplyBlockStyle(paragraph, model);
            if (model.Kind == ArticleTextKind.Bullet)
            {
                paragraph.Inlines.Add(new Run { Text = "• " });
            }
            else if (model.Kind == ArticleTextKind.Ordered)
            {
                paragraph.Inlines.Add(new Run { Text = Math.Max(model.ListOrder, 1) + ". " });
            }

            foreach (ArticleInlineModel inline in model.Inlines)
            {
                if (string.IsNullOrEmpty(inline.Text))
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(inline.Link))
                {
                    Run run = new Run { Text = inline.Text };
                    ApplyInlineStyle(run, inline);
                    paragraph.Inlines.Add(run);
                }
                else
                {
                    Hyperlink hyperlink = new Hyperlink();
                    Run run = new Run { Text = inline.Text };
                    ApplyInlineStyle(run, inline);
                    hyperlink.Inlines.Add(run);
                    string link = inline.Link;
                    hyperlink.Click += (sender, args) => LinkClicked?.Invoke(this, link);
                    paragraph.Inlines.Add(hyperlink);
                }
            }
            richText.Blocks.Add(paragraph);
        }

        private static void ApplyBlockStyle(Paragraph paragraph, ArticleTextBlockModel model)
        {
            if (model.Kind == ArticleTextKind.Heading)
            {
                paragraph.FontWeight = FontWeights.SemiBold;
                switch (model.HeadingLevel)
                {
                    case 1:
                        paragraph.FontSize = 30;
                        break;
                    case 2:
                        paragraph.FontSize = 24;
                        break;
                    case 3:
                        paragraph.FontSize = 21;
                        break;
                    default:
                        paragraph.FontSize = 18;
                        break;
                }
            }
            if (model.Kind == ArticleTextKind.Quote)
            {
                paragraph.Margin = new Thickness(16, 0, 0, 0);
                paragraph.Foreground = new SolidColorBrush(Colors.Gray);
            }
            switch ((model.Alignment ?? string.Empty).ToLowerInvariant())
            {
                case "center":
                    paragraph.TextAlignment = TextAlignment.Center;
                    break;
                case "right":
                    paragraph.TextAlignment = TextAlignment.Right;
                    break;
                case "justify":
                    paragraph.TextAlignment = TextAlignment.Justify;
                    break;
                default:
                    paragraph.TextAlignment = TextAlignment.Left;
                    break;
            }
        }

        private static void ApplyInlineStyle(Run run, ArticleInlineModel model)
        {
            run.FontWeight = model.Bold ? FontWeights.Bold : FontWeights.Normal;
            run.FontStyle = model.Italic ? FontStyle.Italic : FontStyle.Normal;
            run.TextDecorations = model.Strike ? TextDecorations.Strikethrough : TextDecorations.None;
            Color color;
            if (TryParseColor(model.Color, out color))
            {
                run.Foreground = new SolidColorBrush(color);
            }
        }

        private static bool TryParseColor(string value, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(value) || value[0] != '#')
            {
                return false;
            }
            string hex = value.Substring(1);
            if (hex.Length == 3)
            {
                hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            }
            uint parsed;
            if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out parsed))
            {
                color = ColorHelper.FromArgb(255, (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed);
                return true;
            }
            if (hex.Length == 8 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out parsed))
            {
                color = ColorHelper.FromArgb((byte)(parsed >> 24), (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed);
                return true;
            }
            return false;
        }
    }
}
