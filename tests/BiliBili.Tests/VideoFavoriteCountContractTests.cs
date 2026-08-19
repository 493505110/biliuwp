using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class VideoFavoriteCountContractTests
    {
        [TestMethod]
        public void FavoriteMenu_DoesNotReplaceLocalCountWithStaleRefresh()
        {
            var source = TestRepository.ReadFile("BiliBili.UWP/Pages/VideoViewPage.xaml.cs");
            var handler = TestRepository.Between(
                source,
                "private async void Video_ListView_Favbox_ItemClick",
                "private void list_About_ItemClick");

            const string update = "favorite.media_count = Math.Max(0, favorite.media_count + (isFavorite ? -1 : 1));";
            var updateIndex = handler.IndexOf(update, StringComparison.Ordinal);
            var refreshIndex = handler.IndexOf("await GetFavBox();", StringComparison.Ordinal);

            Assert.IsTrue(updateIndex >= 0, "收藏成功后必须按收藏或取消收藏方向更新当前项数量。");
            Assert.IsTrue(refreshIndex < 0, "成功操作后不能用可能过期的收藏夹刷新覆盖本地数量。");
            StringAssert.Contains(handler, "Video_ListView_Favbox.IsEnabled = true;");
        }

        [TestMethod]
        public void FavboxModel_MediaCountNotifiesBindingChanges()
        {
            var source = TestRepository.ReadFile("BiliBili.UWP/Models/VideoInfoModels.cs");

            StringAssert.Contains(source, "public class FavboxModel : INotifyPropertyChanged");
            StringAssert.Contains(source, "PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(\"media_count\"))");
            StringAssert.Contains(source, "PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(\"fav_state\"))");
        }

    }
}
