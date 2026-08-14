using BiliBili.UWP.Helper;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages
{
    public sealed partial class CommentFilterManagePage : Page
    {
        public CommentFilterManagePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.New)
            {
                LoadSetting();
            }
        }

        private void LoadSetting()
        {
            list_FilterWord.Items.Clear();
            var words = SettingHelper.Get_CommentFilterWords();
            if (!string.IsNullOrEmpty(words))
            {
                foreach (var word in words.Split('|').Where(x => !string.IsNullOrEmpty(x)))
                {
                    list_FilterWord.Items.Add(word);
                }
            }
        }

        private void btn_AddFilterWord_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txt_FilterWord.Text))
            {
                txt_FilterWord.Text = "关键词不能为空";
                return;
            }

            SettingHelper.Set_CommentFilterWords(SettingHelper.Get_CommentFilterWords() + "|" + txt_FilterWord.Text);
            list_FilterWord.Items.Add(txt_FilterWord.Text);
            txt_FilterWord.Text = string.Empty;
        }

        private void btn_DeleteFilterWord_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = list_FilterWord.SelectedItems.Cast<string>().ToList();
            if (selectedItems.Count == 0)
            {
                return;
            }

            var words = SettingHelper.Get_CommentFilterWords();
            foreach (var item in selectedItems)
            {
                list_FilterWord.Items.Remove(item);
                words = string.Join("|", words.Split('|').Where(x => x != item));
            }
            SettingHelper.Set_CommentFilterWords(words);
        }
    }
}
