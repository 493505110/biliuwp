using BiliBili.UWP.Controls;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages.FindMore
{
    public sealed partial class ArticleContentPage : Page
    {
        private readonly ArticleVM viewModel = new ArticleVM();
        private long articleId;

        public ArticleContentPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (!ArticleParameterParser.TryParse(e.Parameter, out articleId))
            {
                ShowError("无法打开无效的专栏地址");
                return;
            }
            await LoadArticleAsync();
        }

        private async System.Threading.Tasks.Task LoadArticleAsync()
        {
            ShowLoading();
            await viewModel.LoadAsync(articleId);
            RenderState();
        }

        private void ShowLoading()
        {
            articleScroll.Visibility = Visibility.Collapsed;
            errorPanel.Visibility = Visibility.Collapsed;
            pr_Load.Visibility = Visibility.Visible;
            pr_Load.IsActive = true;
        }

        private void RenderState()
        {
            if (viewModel.Loading)
            {
                ShowLoading();
                return;
            }
            pr_Load.IsActive = false;
            pr_Load.Visibility = Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(viewModel.ErrorMessage))
            {
                ShowError(viewModel.ErrorMessage);
                return;
            }
            if (viewModel.Article == null)
            {
                ShowError("专栏数据解析失败");
                return;
            }

            ArticleDataModel article = viewModel.Article;
            txt_Header.Text = string.IsNullOrWhiteSpace(article.title) ? "专栏" : article.title;
            articleTitle.Text = article.title ?? string.Empty;
            authorName.Text = article.author == null ? string.Empty : article.author.name ?? string.Empty;
            articleMeta.Text = BuildMeta(article);
            articleStats.Text = BuildStats(article.stats);
            authorAvatar.ImageSource = CreateImageSource(article.author == null ? null : article.author.face);
            articleBlocks.ItemsSource = viewModel.Blocks;
            errorPanel.Visibility = Visibility.Collapsed;
            articleScroll.Visibility = Visibility.Visible;
            articleScroll.ChangeView(null, 0, null, true);
        }

        private static string BuildMeta(ArticleDataModel article)
        {
            string date = article.publish_time > 0
                ? Utils.TimestampToDatetime(article.publish_time).ToString("yyyy-MM-dd")
                : string.Empty;
            string category = article.category == null ? string.Empty : article.category.name ?? string.Empty;
            if (string.IsNullOrEmpty(date))
            {
                return category;
            }
            return string.IsNullOrEmpty(category) ? date : date + " · " + category;
        }

        private static string BuildStats(ArticleStatsModel stats)
        {
            if (stats == null)
            {
                return string.Empty;
            }
            return "阅读 " + stats.view + " · 点赞 " + stats.like + " · 收藏 " + stats.favorite;
        }

        private static BitmapImage CreateImageSource(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) ? new BitmapImage(uri) : null;
        }

        private void ShowError(string message)
        {
            pr_Load.IsActive = false;
            pr_Load.Visibility = Visibility.Collapsed;
            articleScroll.Visibility = Visibility.Collapsed;
            errorText.Text = string.IsNullOrWhiteSpace(message) ? "专栏加载失败" : message;
            errorPanel.Visibility = Visibility.Visible;
        }

        private async void ArticleText_LinkClicked(object sender, string link)
        {
            await OpenLinkAsync(link);
        }

        private async void EmbedCard_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            await OpenLinkAsync(button == null ? null : button.Tag as string);
        }

        private async System.Threading.Tasks.Task OpenLinkAsync(string link)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link, UriKind.Absolute, out uri))
            {
                Utils.ShowMessageToast("无法打开无效链接");
                return;
            }
            if (await MessageCenter.HandelUrl(link))
            {
                return;
            }
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                Utils.ShowMessageToast("不支持打开的链接：" + link);
                return;
            }

            MessageDialog dialog = new MessageDialog("是否调用外部浏览器打开此链接？");
            UICommand confirm = new UICommand("确定");
            UICommand cancel = new UICommand("取消");
            dialog.Commands.Add(confirm);
            dialog.Commands.Add(cancel);
            dialog.CancelCommandIndex = 1;
            dialog.DefaultCommandIndex = 0;
            IUICommand selected = await dialog.ShowAsync();
            if (selected == confirm)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private void ArticleImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Image image = sender as Image;
            Grid parent = image == null ? null : image.Parent as Grid;
            if (parent == null || parent.Children.Count < 2)
            {
                return;
            }
            image.Visibility = Visibility.Collapsed;
            parent.Children[1].Visibility = Visibility.Visible;
        }

        private async void Retry_Click(object sender, RoutedEventArgs e)
        {
            await LoadArticleAsync();
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void btn_Share_Click(object sender, RoutedEventArgs e)
        {
            if (articleId <= 0)
            {
                return;
            }
            string url = "https://www.bilibili.com/read/cv" + articleId;
            DataPackage package = new DataPackage();
            package.SetText(url);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            Utils.ShowMessageToast("已将地址复制到剪切板", 3000);
        }
    }

    public class ArticleBlockTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate ImageTemplate { get; set; }
        public DataTemplate SeparatorTemplate { get; set; }
        public DataTemplate EmbedTemplate { get; set; }
        public DataTemplate UnknownTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            ArticleBlockModel block = item as ArticleBlockModel;
            if (block == null)
            {
                return UnknownTemplate;
            }
            switch (block.Type)
            {
                case ArticleBlockType.Text:
                    return TextTemplate;
                case ArticleBlockType.Image:
                    return ImageTemplate;
                case ArticleBlockType.Separator:
                    return SeparatorTemplate;
                case ArticleBlockType.Embed:
                    return EmbedTemplate;
                default:
                    return UnknownTemplate;
            }
        }
    }
}
