using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.UI.Xaml;

namespace BiliBili.UWP.Models
{
    // ===== Polymer API 响应模型（仅保留展示所需字段）=====

    public class SpaceDynamicResp
    {
        public bool has_more { get; set; }
        public string offset { get; set; }
        public List<SpaceDynamicItem> items { get; set; }
    }

    public class SpaceDynamicItem
    {
        public string type { get; set; }
        public string id_str { get; set; }
        public bool visible { get; set; }
        public SpaceDynModules modules { get; set; }
        //转发动态的原始内容
        public SpaceDynamicItem orig { get; set; }
        // 详情页评论信息（/x/polymer/web-dynamic/v1/detail 返回）
        public SpaceDynBasic basic { get; set; }
    }

    public class SpaceDynBasic
    {
        public string comment_id_str { get; set; }
        public int comment_type { get; set; }
        public string rid_str { get; set; }
    }

    public class SpaceDynModules
    {
        public SpaceDynAuthor module_author { get; set; }
        public SpaceDynDynamic module_dynamic { get; set; }
        public SpaceDynStat module_stat { get; set; }
    }

    public class SpaceDynAuthor
    {
        public string name { get; set; }
        //头像URL，polymer接口此字段在顶层
        public string face { get; set; }
        public long mid { get; set; }
        public long pub_ts { get; set; }
        //已格式化的发布时间，如"昨天 18:30"
        public string pub_time { get; set; }
    }

    public class SpaceDynDynamic
    {
        public SpaceDynDesc desc { get; set; }
        public SpaceDynMajor major { get; set; }
    }

    public class SpaceDynDesc
    {
        public string text { get; set; }
    }

    public class SpaceDynMajor
    {
        public string type { get; set; }
        public SpaceDynArchive archive { get; set; }
        public SpaceDynArchive ugc_season { get; set; }
        public SpaceDynDraw draw { get; set; }
        public SpaceDynArticle article { get; set; }
        public SpaceDynOpus opus { get; set; }
        public SpaceDynPgc pgc { get; set; }
    }

    public class SpaceDynArchive
    {
        public string aid { get; set; }
        public string bvid { get; set; }
        public string cover { get; set; }
        public string title { get; set; }
        public string desc { get; set; }
        public string duration_text { get; set; }
        public string jump_url { get; set; }
        public SpaceDynStat2 stat { get; set; }
    }

    public class SpaceDynStat2
    {
        public string play { get; set; }
        public string danmaku { get; set; }
    }

    public class SpaceDynDraw
    {
        public List<SpaceDynDrawItem> items { get; set; }
    }

    public class SpaceDynDrawItem
    {
        public string src { get; set; }
        /// <summary>opus.pics 使用 url 字段，draw.items 使用 src 字段</summary>
        public string url { get; set; }
        public double width { get; set; }
        public double height { get; set; }
    }

    public class SpaceDynArticle
    {
        public long id { get; set; }
        public string title { get; set; }
        public string desc { get; set; }
        public List<string> covers { get; set; }
        public string jump_url { get; set; }
        public string label { get; set; }
    }

    public class SpaceDynOpus
    {
        public string jump_url { get; set; }
        public List<SpaceDynDrawItem> pics { get; set; }
        public SpaceDynOpusSummary summary { get; set; }
        public string title { get; set; }
    }

    public class SpaceDynOpusSummary
    {
        public string text { get; set; }
    }

    public class SpaceDynPgc
    {
        public string cover { get; set; }
        public string title { get; set; }
        public string jump_url { get; set; }
        public int epid { get; set; }
        public SpaceDynStat2 stat { get; set; }
    }

    public class SpaceDynStat
    {
        public SpaceDynStatCount comment { get; set; }
        public SpaceDynStatCount forward { get; set; }
        public SpaceDynStatCount like { get; set; }
    }

    public class SpaceDynStatCount
    {
        public int count { get; set; }
    }

    // ===== 动态详情接口响应（/x/polymer/web-dynamic/v1/detail）=====

    public class SpaceDynDetailData
    {
        public SpaceDynamicItem item { get; set; }
    }

    // ===== 转发列表接口响应（/x/polymer/web-dynamic/v1/detail/forward）=====

    public class SpaceDynForwardResp
    {
        public bool has_more { get; set; }
        public List<SpaceDynForwardItem> items { get; set; }
        public string offset { get; set; }
        public int total { get; set; }
    }

    public class SpaceDynForwardItem
    {
        public string id_str { get; set; }
        public string pub_time { get; set; }
        public SpaceDynForwardUser user { get; set; }
        public SpaceDynForwardDesc desc { get; set; }
    }

    public class SpaceDynForwardUser
    {
        public string face { get; set; }
        public string name { get; set; }
        public long mid { get; set; }
    }

    public class SpaceDynForwardDesc
    {
        public string text { get; set; }
    }

    /// <summary>转发列表行 ViewModel，供 ls_repost x:Bind 绑定。
    /// 只展示转发人，不展示 desc.text（那是整条转发链的拼接文本）</summary>
    public class SpaceDynForwardItemVM
    {
        public string FaceThumb { get; set; }
        public string Name { get; set; }
        public long Mid { get; set; }
        public string PubTime { get; set; }

        public static SpaceDynForwardItemVM From(SpaceDynForwardItem item)
        {
            var face = item.user?.face ?? "";
            return new SpaceDynForwardItemVM
            {
                FaceThumb = string.IsNullOrEmpty(face) ? "" : face + "@36w_36h.jpg",
                Name = item.user?.name ?? "",
                Mid = item.user?.mid ?? 0,
                PubTime = item.pub_time ?? ""
            };
        }
    }

    // ===== ViewModel（供 XAML x:Bind 绑定）=====

    public class SpaceDynItemVM : INotifyPropertyChanged
    {
        public string IdStr { get; set; }
        public string DynType { get; set; }

        // 作者
        public string AuthorName { get; set; }
        public string AuthorFaceRaw { get; set; }
        public string AuthorFace => string.IsNullOrEmpty(AuthorFaceRaw)
            ? "" : AuthorFaceRaw + "@64w_64h.jpg";
        public long AuthorMid { get; set; }
        public string PubTime { get; set; }

        // 文字内容
        public string Text { get; set; }
        public Visibility TextVisible => !string.IsNullOrEmpty(Text)
            ? Visibility.Visible : Visibility.Collapsed;

        // 视频
        public Visibility VideoVisible { get; set; } = Visibility.Collapsed;
        public string VideoCover { get; set; }
        public string VideoTitle { get; set; }
        public string VideoDuration { get; set; }
        public string VideoPlay { get; set; }
        public string VideoAid { get; set; }
        /// <summary>PGC(番剧)类型的ep号，跳转用BanInfoPage</summary>
        public int PgcEpId { get; set; }

        // 图片（最多9张）
        public Visibility ImagesVisible { get; set; } = Visibility.Collapsed;
        public ObservableCollection<string> Images { get; set; } = new ObservableCollection<string>();
        public List<string> ImagesRaw { get; set; } = new List<string>();

        // 文章
        public Visibility ArticleVisible { get; set; } = Visibility.Collapsed;
        public string ArticleCover { get; set; }
        public Visibility ArticleCoverVisible => !string.IsNullOrEmpty(ArticleCover)
            ? Visibility.Visible : Visibility.Collapsed;
        public string ArticleTitle { get; set; }
        public string ArticleDesc { get; set; }
        public long ArticleId { get; set; }

        // 转发摘要（仅一行简述原内容）
        public Visibility ForwardVisible { get; set; } = Visibility.Collapsed;
        public string ForwardSummary { get; set; }

        // 统计
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ForwardCount { get; set; }
        public string LikeStr => LikeCount.ToString();
        public string CommentStr => CommentCount.ToString();
        public string ForwardStr => ForwardCount.ToString();

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 把 Polymer API 返回的一条动态转成 ViewModel
        /// </summary>
        public static SpaceDynItemVM FromItem(SpaceDynamicItem item)
        {
            var vm = new SpaceDynItemVM()
            {
                IdStr = item.id_str,
                DynType = item.type ?? ""
            };

            var author = item.modules?.module_author;
            if (author != null)
            {
                vm.AuthorName = author.name ?? "";
                vm.AuthorFaceRaw = author.face ?? "";
                vm.AuthorMid = author.mid;
                vm.PubTime = string.IsNullOrEmpty(author.pub_time)
                    ? DateFromTs(author.pub_ts) : author.pub_time;
            }

            var dyn = item.modules?.module_dynamic;
            vm.Text = dyn?.desc?.text ?? "";

            var major = dyn?.major;
            if (major != null)
            {
                switch (major.type)
                {
                    case "MAJOR_TYPE_ARCHIVE":
                        FillVideo(vm, major.archive);
                        break;
                    case "MAJOR_TYPE_UGC_SEASON":
                        FillVideo(vm, major.ugc_season);
                        break;
                    case "MAJOR_TYPE_PGC":
                        FillPgc(vm, major.pgc);
                        break;
                    case "MAJOR_TYPE_DRAW":
                        FillDraw(vm, major.draw?.items);
                        break;
                    case "MAJOR_TYPE_OPUS":
                        FillOpus(vm, major.opus);
                        break;
                    case "MAJOR_TYPE_ARTICLE":
                        FillArticle(vm, major.article);
                        break;
                }
            }

            // 转发动态：把原始内容摘要化
            if (item.type == "DYNAMIC_TYPE_FORWARD" && item.orig != null)
            {
                vm.ForwardVisible = Visibility.Visible;
                var origAuthor = item.orig.modules?.module_author?.name ?? "";
                var origDesc = item.orig.modules?.module_dynamic?.desc?.text ?? "";
                // 原始内容也可能是视频/文章，拼一行简述
                var origMajor = item.orig.modules?.module_dynamic?.major;
                if (origMajor != null)
                {
                    var title = origMajor.archive?.title
                        ?? origMajor.ugc_season?.title
                        ?? origMajor.article?.title
                        ?? origMajor.pgc?.title ?? "";
                    if (!string.IsNullOrEmpty(title))
                    {
                        origDesc = string.IsNullOrEmpty(origDesc)
                            ? title : origDesc + "\n" + title;
                    }
                }
                vm.ForwardSummary = string.IsNullOrEmpty(origAuthor)
                    ? origDesc
                    : $"@{origAuthor}：{origDesc}";
                if (vm.ForwardSummary.Length > 100)
                    vm.ForwardSummary = vm.ForwardSummary.Substring(0, 100) + "…";
            }

            var stat = item.modules?.module_stat;
            if (stat != null)
            {
                vm.LikeCount = stat.like?.count ?? 0;
                vm.CommentCount = stat.comment?.count ?? 0;
                vm.ForwardCount = stat.forward?.count ?? 0;
            }

            return vm;
        }

        private static void FillVideo(SpaceDynItemVM vm, SpaceDynArchive a)
        {
            if (a == null) return;
            vm.VideoVisible = Visibility.Visible;
            vm.VideoCover = a.cover != null ? a.cover + "@200w.jpg" : "";
            vm.VideoTitle = a.title ?? "";
            vm.VideoDuration = a.duration_text ?? "";
            vm.VideoPlay = a.stat?.play ?? "";
            vm.VideoAid = a.aid ?? "";
        }

        private static void FillPgc(SpaceDynItemVM vm, SpaceDynPgc p)
        {
            if (p == null) return;
            vm.VideoVisible = Visibility.Visible;
            vm.VideoCover = p.cover != null ? p.cover + "@200w.jpg" : "";
            vm.VideoTitle = p.title ?? "";
            vm.VideoPlay = p.stat?.play ?? "";
            vm.PgcEpId = p.epid;
            //VideoAid 留空，跳转时通过PgcEpId走番剧页
        }

        private static void FillDraw(SpaceDynItemVM vm, List<SpaceDynDrawItem> pics)
        {
            if (pics == null || pics.Count == 0) return;
            vm.ImagesVisible = Visibility.Visible;
            int max = Math.Min(pics.Count, 9);
            for (int i = 0; i < max; i++)
            {
                //draw.items 用 src，opus.pics 用 url
                var raw = pics[i].src ?? pics[i].url ?? "";
                vm.ImagesRaw.Add(raw);
                vm.Images.Add(raw + "@300w_200h_1e_1c.jpg");
            }
        }

        private static void FillOpus(SpaceDynItemVM vm, SpaceDynOpus o)
        {
            if (o == null) return;
            if (o.pics != null && o.pics.Count > 0) FillDraw(vm, o.pics);
            if (!string.IsNullOrEmpty(o.summary?.text) && string.IsNullOrEmpty(vm.Text))
                vm.Text = o.summary.text;
        }

        private static void FillArticle(SpaceDynItemVM vm, SpaceDynArticle a)
        {
            if (a == null) return;
            vm.ArticleVisible = Visibility.Visible;
            vm.ArticleTitle = a.title ?? "";
            vm.ArticleDesc = a.desc ?? "";
            vm.ArticleId = a.id;
            vm.ArticleCover = a.covers?.Count > 0 ? a.covers[0] + "@200w.jpg" : "";
        }

        private static string DateFromTs(long ts)
        {
            if (ts <= 0) return "";
            var dt = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime();
            var span = DateTimeOffset.Now - dt;
            if (span.TotalMinutes < 1) return "刚刚";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}分钟前";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours}小时前";
            if (span.TotalDays < 2) return "昨天 " + dt.ToString("HH:mm");
            return dt.ToString("MM-dd HH:mm");
        }
    }
}