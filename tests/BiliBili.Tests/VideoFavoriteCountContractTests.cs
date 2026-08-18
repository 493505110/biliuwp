using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class VideoFavoriteCountContractTests
    {
        [TestMethod]
        public void FavoriteMenu_DoesNotReplaceLocalCountWithStaleRefresh()
        {
            var source = ReadRepositoryFile("BiliBili.UWP/Pages/VideoViewPage.xaml.cs");
            var handler = Between(
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
            var source = ReadRepositoryFile("BiliBili.UWP/Models/VideoInfoModels.cs");

            StringAssert.Contains(source, "public class FavboxModel : INotifyPropertyChanged");
            StringAssert.Contains(source, "PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(\"media_count\"))");
            StringAssert.Contains(source, "PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(\"fav_state\"))");
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                directory = directory.Parent;
            }

            Assert.Fail($"找不到仓库文件：{relativePath}");
            return null;
        }

        private static string Between(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);

            Assert.IsTrue(start >= 0, $"找不到起始标记：{startMarker}");
            Assert.IsTrue(end > start, $"找不到结束标记：{endMarker}");
            return source.Substring(start, end - start);
        }
    }
}
