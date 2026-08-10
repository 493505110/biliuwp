using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BiliBili.UWP.Models;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace BiliBili.UWP.Modules
{
    public class ArticleContentParser
    {
        private static readonly Regex CssColorRegex = new Regex(
            @"(?:^|;)\s*color\s*:\s*([^;]+)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public IReadOnlyList<ArticleBlockModel> Parse(ArticleDataModel article)
        {
            if (article == null)
            {
                throw new ArgumentNullException(nameof(article));
            }

            if (article.type == 0)
            {
                return ParseHtml(article.content);
            }

            if (article.type == 3 || article.type == 4)
            {
                return ParseDeltaOrOpus(article.content, article.opus);
            }

            throw new FormatException("不支持的专栏正文类型：" + article.type);
        }

        private static IReadOnlyList<ArticleBlockModel> ParseDeltaOrOpus(string content, JObject opus)
        {
            try
            {
                IReadOnlyList<ArticleBlockModel> blocks = ParseDelta(content);
                if (blocks.Count > 0)
                {
                    return blocks;
                }
            }
            catch
            {
            }

            return ParseOpus(opus);
        }

        private static IReadOnlyList<ArticleBlockModel> ParseDelta(string content)
        {
            JObject root = JObject.Parse(content ?? string.Empty);
            JArray operations = root["ops"] as JArray;
            if (operations == null)
            {
                return new List<ArticleBlockModel>();
            }

            List<ArticleBlockModel> blocks = new List<ArticleBlockModel>();
            ArticleTextBlockModel pending = new ArticleTextBlockModel { Kind = ArticleTextKind.Paragraph };
            int orderedItem = 0;
            foreach (JObject operation in operations.OfType<JObject>())
            {
                JToken insert = operation["insert"];
                JObject attributes = operation["attributes"] as JObject ?? operation["attribute"] as JObject;
                if (insert == null)
                {
                    continue;
                }

                if (insert.Type == JTokenType.String)
                {
                    string[] parts = insert.Value<string>().Split('\n');
                    for (int index = 0; index < parts.Length; index++)
                    {
                        AppendDeltaInline(pending, parts[index], attributes);
                        if (index < parts.Length - 1)
                        {
                            ApplyDeltaBlockAttributes(pending, attributes, ref orderedItem);
                            FlushPendingText(blocks, ref pending);
                        }
                    }
                    continue;
                }

                JObject objectInsert = insert as JObject;
                if (objectInsert == null || !objectInsert.Properties().Any())
                {
                    continue;
                }

                FlushPendingText(blocks, ref pending);
                JProperty property = objectInsert.Properties().First();
                AddDeltaObject(blocks, property.Name, property.Value as JObject);
            }

            FlushPendingText(blocks, ref pending);
            return blocks;
        }

        private static void AppendDeltaInline(ArticleTextBlockModel block, string text, JObject attributes)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            InlineStyle style = new InlineStyle
            {
                Bold = GetBool(attributes, "bold"),
                Italic = GetBool(attributes, "italic"),
                Strike = GetBool(attributes, "strike"),
                Color = GetString(attributes, "color"),
                Link = GetString(attributes, "link")
            };
            AppendInline(block.Inlines, text, style);
        }

        private static void ApplyDeltaBlockAttributes(
            ArticleTextBlockModel block,
            JObject attributes,
            ref int orderedItem)
        {
            int header = GetInt(attributes, "header");
            string list = GetString(attributes, "list");
            if (header >= 1 && header <= 6)
            {
                block.Kind = ArticleTextKind.Heading;
                block.HeadingLevel = header;
                orderedItem = 0;
            }
            else if (GetBool(attributes, "blockquote"))
            {
                block.Kind = ArticleTextKind.Quote;
                orderedItem = 0;
            }
            else if (string.Equals(list, "ordered", StringComparison.OrdinalIgnoreCase))
            {
                block.Kind = ArticleTextKind.Ordered;
                block.ListLevel = 1;
                block.ListOrder = ++orderedItem;
            }
            else if (string.Equals(list, "bullet", StringComparison.OrdinalIgnoreCase))
            {
                block.Kind = ArticleTextKind.Bullet;
                block.ListLevel = 1;
                orderedItem = 0;
            }
            else
            {
                orderedItem = 0;
            }

            block.Alignment = GetString(attributes, "align");
        }

        private static void FlushPendingText(
            IList<ArticleBlockModel> blocks,
            ref ArticleTextBlockModel pending)
        {
            if (pending.Inlines.Any(item => !string.IsNullOrWhiteSpace(item.Text)))
            {
                blocks.Add(pending);
            }
            pending = new ArticleTextBlockModel { Kind = ArticleTextKind.Paragraph };
        }

        private static void AddDeltaObject(IList<ArticleBlockModel> blocks, string name, JObject value)
        {
            value = value ?? new JObject();
            if (name == "native-image")
            {
                blocks.Add(new ArticleImageBlockModel
                {
                    Url = GetString(value, "url"),
                    Alt = GetString(value, "alt"),
                    Width = GetInt(value, "width"),
                    Height = GetInt(value, "height")
                });
                return;
            }
            if (name == "cut-off")
            {
                blocks.Add(new ArticleSeparatorBlockModel());
                return;
            }

            ArticleEmbedType embedType;
            string link;
            string id = GetString(value, "id") ?? string.Empty;
            switch (name)
            {
                case "video-card":
                    embedType = ArticleEmbedType.Video;
                    link = "https://www.bilibili.com/video/" + id;
                    break;
                case "article-card":
                    embedType = ArticleEmbedType.Article;
                    link = "https://www.bilibili.com/read/" + id;
                    break;
                case "vote-card":
                    embedType = ArticleEmbedType.Vote;
                    link = "https://t.bilibili.com/vote/h5/index/#/result?vote_id=" + StripPrefix(id, "vote");
                    break;
                case "live-card":
                    embedType = ArticleEmbedType.Live;
                    link = "https://live.bilibili.com/" + StripPrefix(id, "lv");
                    break;
                default:
                    blocks.Add(new ArticleUnknownBlockModel { Description = "暂不支持的专栏节点：" + name });
                    return;
            }

            blocks.Add(new ArticleEmbedBlockModel
            {
                EmbedType = embedType,
                Id = id,
                CoverUrl = GetString(value, "url"),
                Title = GetString(value, "alt"),
                Link = link
            });
        }

        private static IReadOnlyList<ArticleBlockModel> ParseOpus(JObject opus)
        {
            List<ArticleBlockModel> blocks = new List<ArticleBlockModel>();
            JArray paragraphs = opus == null ? null : opus.SelectToken("content.paragraphs") as JArray;
            if (paragraphs == null)
            {
                return blocks;
            }

            foreach (JToken paragraphToken in paragraphs)
            {
                JObject paragraph = paragraphToken as JObject;
                int type = GetInt(paragraph, "para_type");
                try
                {
                    if (type == 1 || type == 6)
                    {
                        AddOpusTextBlock(blocks, paragraph, type);
                    }
                    else if (type == 2)
                    {
                        AddOpusImages(blocks, paragraph);
                    }
                    else
                    {
                        blocks.Add(new ArticleUnknownBlockModel
                        {
                            Description = "暂不支持的 Opus 段落类型：" + type
                        });
                    }
                }
                catch
                {
                    blocks.Add(new ArticleUnknownBlockModel
                    {
                        Description = "无法解析的 Opus 段落类型：" + type
                    });
                }
            }
            return blocks;
        }

        private static void AddOpusTextBlock(
            IList<ArticleBlockModel> blocks,
            JObject paragraph,
            int paragraphType)
        {
            JArray nodes = paragraph.SelectToken("text.nodes") as JArray;
            if (nodes == null)
            {
                throw new FormatException("Opus 文本段落缺少节点");
            }
            string text = nodes == null
                ? string.Empty
                : string.Concat(nodes.Select(node => (string)node.SelectToken("word.words") ?? string.Empty));
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException("Opus 文本段落为空");
            }

            ArticleTextBlockModel block = new ArticleTextBlockModel();
            if (paragraphType == 6)
            {
                int order = GetInt(paragraph.SelectToken("format.list_format") as JObject, "order");
                block.Kind = order > 0 ? ArticleTextKind.Ordered : ArticleTextKind.Bullet;
                block.ListLevel = 1;
                block.ListOrder = Math.Max(order, 0);
            }
            else
            {
                int fontLevel = nodes == null
                    ? 0
                    : nodes.Select(node => GetInt(node["word"] as JObject, "font_level")).FirstOrDefault(value => value > 0);
                int fontSize = nodes == null
                    ? 0
                    : nodes.Select(node => GetInt(node["word"] as JObject, "font_size")).FirstOrDefault(value => value > 0);
                block.Kind = fontLevel > 0 || fontSize > 0 ? ArticleTextKind.Heading : ArticleTextKind.Paragraph;
                block.HeadingLevel = fontLevel > 0 ? Math.Min(fontLevel, 6) : (fontSize > 0 ? 2 : 0);
            }
            block.Inlines.Add(new ArticleInlineModel { Text = text });
            blocks.Add(block);
        }

        private static void AddOpusImages(IList<ArticleBlockModel> blocks, JObject paragraph)
        {
            JArray pictures = paragraph.SelectToken("pic.pics") as JArray;
            if (pictures == null || pictures.Count == 0)
            {
                throw new FormatException("Opus 图片段落为空");
            }
            foreach (JObject picture in pictures.OfType<JObject>())
            {
                blocks.Add(new ArticleImageBlockModel
                {
                    Url = GetString(picture, "url"),
                    Alt = GetString(picture, "alt"),
                    Width = GetInt(picture, "width"),
                    Height = GetInt(picture, "height")
                });
            }
        }

        private static string StripPrefix(string value, string prefix)
        {
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(prefix.Length)
                : value;
        }

        private static string GetString(JObject value, string name)
        {
            JToken token = value == null ? null : value[name];
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        private static int GetInt(JObject value, string name)
        {
            int result;
            return int.TryParse(GetString(value, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                ? result
                : 0;
        }

        private static bool GetBool(JObject value, string name)
        {
            bool result;
            return bool.TryParse(GetString(value, name), out result) && result;
        }

        private static IReadOnlyList<ArticleBlockModel> ParseHtml(string html)
        {
            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(html ?? string.Empty);
            List<ArticleBlockModel> blocks = new List<ArticleBlockModel>();
            ParseBlockChildren(document.DocumentNode, blocks);
            return blocks;
        }

        private static void ParseBlockChildren(HtmlNode parent, IList<ArticleBlockModel> blocks)
        {
            foreach (HtmlNode node in parent.ChildNodes)
            {
                if (node.NodeType == HtmlNodeType.Text)
                {
                    AddLooseText(node, blocks);
                    continue;
                }

                if (node.NodeType != HtmlNodeType.Element)
                {
                    continue;
                }

                string name = node.Name.ToLowerInvariant();
                if (name == "script" || name == "style" || name == "noscript")
                {
                    continue;
                }

                int headingLevel;
                if (TryGetHeadingLevel(name, out headingLevel))
                {
                    AddTextBlock(node, ArticleTextKind.Heading, headingLevel, 0, 0, blocks);
                    continue;
                }

                switch (name)
                {
                    case "p":
                        AddTextBlock(node, ArticleTextKind.Paragraph, 0, 0, 0, blocks);
                        break;
                    case "blockquote":
                        AddTextBlock(node, ArticleTextKind.Quote, 0, 0, 0, blocks);
                        break;
                    case "ol":
                        AddList(node, ArticleTextKind.Ordered, blocks);
                        break;
                    case "ul":
                        AddList(node, ArticleTextKind.Bullet, blocks);
                        break;
                    case "li":
                        AddTextBlock(node, ArticleTextKind.Bullet, 0, GetListLevel(node), 0, blocks);
                        break;
                    case "img":
                        AddImage(node, blocks);
                        break;
                    case "hr":
                        blocks.Add(new ArticleSeparatorBlockModel());
                        break;
                    case "br":
                        break;
                    default:
                        if (ContainsBlockContent(node))
                        {
                            ParseBlockChildren(node, blocks);
                        }
                        else
                        {
                            AddTextBlock(node, ArticleTextKind.Paragraph, 0, 0, 0, blocks);
                        }
                        break;
                }
            }
        }

        private static void AddList(HtmlNode list, ArticleTextKind kind, IList<ArticleBlockModel> blocks)
        {
            int order = 0;
            foreach (HtmlNode item in list.ChildNodes.Where(node =>
                node.NodeType == HtmlNodeType.Element &&
                string.Equals(node.Name, "li", StringComparison.OrdinalIgnoreCase)))
            {
                order++;
                AddTextBlock(
                    item,
                    kind,
                    0,
                    GetListLevel(item),
                    kind == ArticleTextKind.Ordered ? order : 0,
                    blocks);
            }
        }

        private static int GetListLevel(HtmlNode node)
        {
            int level = 0;
            HtmlNode current = node.ParentNode;
            while (current != null)
            {
                if (string.Equals(current.Name, "ol", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current.Name, "ul", StringComparison.OrdinalIgnoreCase))
                {
                    level++;
                }
                current = current.ParentNode;
            }
            return Math.Max(level, 1);
        }

        private static void AddTextBlock(
            HtmlNode node,
            ArticleTextKind kind,
            int headingLevel,
            int listLevel,
            int listOrder,
            IList<ArticleBlockModel> blocks)
        {
            ArticleTextBlockModel block = new ArticleTextBlockModel
            {
                Kind = kind,
                HeadingLevel = headingLevel,
                ListLevel = listLevel,
                ListOrder = listOrder,
                Alignment = GetAlignment(node)
            };
            AppendInlineNodes(node, new InlineStyle(), block.Inlines);
            if (block.Inlines.Any(item => !string.IsNullOrWhiteSpace(item.Text)))
            {
                blocks.Add(block);
            }
        }

        private static void AddLooseText(HtmlNode node, IList<ArticleBlockModel> blocks)
        {
            string value = HtmlEntity.DeEntitize(node.InnerText ?? string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            ArticleTextBlockModel block = new ArticleTextBlockModel
            {
                Kind = ArticleTextKind.Paragraph
            };
            block.Inlines.Add(new ArticleInlineModel { Text = value.Trim() });
            blocks.Add(block);
        }

        private static void AddImage(HtmlNode node, IList<ArticleBlockModel> blocks)
        {
            string url = GetAttribute(node, "data-src");
            if (string.IsNullOrWhiteSpace(url))
            {
                url = GetAttribute(node, "src");
            }
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            blocks.Add(new ArticleImageBlockModel
            {
                Url = HtmlEntity.DeEntitize(url.Trim()),
                Alt = HtmlEntity.DeEntitize(GetAttribute(node, "alt") ?? string.Empty),
                Width = GetPositiveInt(node, "width", "data-w"),
                Height = GetPositiveInt(node, "height", "data-h")
            });
        }

        private static int GetPositiveInt(HtmlNode node, params string[] names)
        {
            foreach (string name in names)
            {
                int value;
                if (int.TryParse(GetAttribute(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                    value > 0)
                {
                    return value;
                }
            }
            return 0;
        }

        private static void AppendInlineNodes(
            HtmlNode parent,
            InlineStyle inheritedStyle,
            IList<ArticleInlineModel> inlines)
        {
            foreach (HtmlNode node in parent.ChildNodes)
            {
                if (node.NodeType == HtmlNodeType.Text)
                {
                    AppendInline(inlines, HtmlEntity.DeEntitize(node.InnerText ?? string.Empty), inheritedStyle);
                    continue;
                }

                if (node.NodeType != HtmlNodeType.Element)
                {
                    continue;
                }

                string name = node.Name.ToLowerInvariant();
                if (name == "script" || name == "style" || name == "noscript" || name == "img")
                {
                    continue;
                }
                if (name == "br")
                {
                    AppendInline(inlines, "\n", inheritedStyle);
                    continue;
                }

                InlineStyle style = inheritedStyle.Clone();
                if (name == "strong" || name == "b")
                {
                    style.Bold = true;
                }
                if (name == "em" || name == "i")
                {
                    style.Italic = true;
                }
                if (name == "s" || name == "del" || name == "strike")
                {
                    style.Strike = true;
                }
                if (name == "a")
                {
                    style.Link = GetAttribute(node, "href");
                }

                string color = name == "font" ? GetAttribute(node, "color") : null;
                Match colorMatch = CssColorRegex.Match(GetAttribute(node, "style") ?? string.Empty);
                if (colorMatch.Success)
                {
                    color = colorMatch.Groups[1].Value.Trim();
                }
                if (!string.IsNullOrWhiteSpace(color))
                {
                    style.Color = color.Trim();
                }

                AppendInlineNodes(node, style, inlines);
            }
        }

        private static void AppendInline(
            IList<ArticleInlineModel> inlines,
            string text,
            InlineStyle style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            ArticleInlineModel last = inlines.LastOrDefault();
            if (last != null &&
                last.Bold == style.Bold &&
                last.Italic == style.Italic &&
                last.Strike == style.Strike &&
                string.Equals(last.Color, style.Color, StringComparison.Ordinal) &&
                string.Equals(last.Link, style.Link, StringComparison.Ordinal))
            {
                last.Text += text;
                return;
            }

            inlines.Add(new ArticleInlineModel
            {
                Text = text,
                Bold = style.Bold,
                Italic = style.Italic,
                Strike = style.Strike,
                Color = style.Color,
                Link = style.Link
            });
        }

        private static bool ContainsBlockContent(HtmlNode node)
        {
            foreach (HtmlNode child in node.ChildNodes)
            {
                if (child.NodeType != HtmlNodeType.Element)
                {
                    continue;
                }

                string name = child.Name.ToLowerInvariant();
                int headingLevel;
                if (TryGetHeadingLevel(name, out headingLevel) ||
                    name == "p" || name == "blockquote" || name == "ol" || name == "ul" ||
                    name == "img" || name == "hr" || name == "div" || name == "figure")
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryGetHeadingLevel(string name, out int level)
        {
            level = 0;
            return name.Length == 2 &&
                name[0] == 'h' &&
                int.TryParse(name.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out level) &&
                level >= 1 && level <= 6;
        }

        private static string GetAlignment(HtmlNode node)
        {
            string align = GetAttribute(node, "align");
            if (!string.IsNullOrWhiteSpace(align))
            {
                return align.Trim().ToLowerInvariant();
            }
            return null;
        }

        private static string GetAttribute(HtmlNode node, string name)
        {
            HtmlAttribute attribute = node.Attributes[name];
            return attribute == null ? null : HtmlEntity.DeEntitize(attribute.Value);
        }

        private class InlineStyle
        {
            public bool Bold { get; set; }
            public bool Italic { get; set; }
            public bool Strike { get; set; }
            public string Color { get; set; }
            public string Link { get; set; }

            public InlineStyle Clone()
            {
                return new InlineStyle
                {
                    Bold = Bold,
                    Italic = Italic,
                    Strike = Strike,
                    Color = Color,
                    Link = Link
                };
            }
        }
    }
}
