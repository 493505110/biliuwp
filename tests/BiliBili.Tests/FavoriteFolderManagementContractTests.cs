using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BiliBili.Tests.TestRepository;

namespace BiliBili.Tests
{
    [TestClass]
    public class FavoriteFolderManagementContractTests
    {
        [TestMethod]
        public void FollowApi_DefinesCreateEditAndDeleteFolderRequests()
        {
            var source = ReadFile("BiliBili.UWP/Api/User/FollowAPI.cs");

            var createFavorite = AssertSignedPostMethod(
                source,
                "public ApiModel CreateFavorite(string title, bool privacy)",
                "baseUrl = \"https://api.bilibili.com/x/v3/fav/folder/add\"");
            StringAssert.Contains(createFavorite, "title={Uri.EscapeDataString(title)}");
            StringAssert.Contains(createFavorite, "privacy={(privacy ? 1 : 0)}");

            var editFavorite = AssertSignedPostMethod(
                source,
                "public ApiModel EditFavorite(string fid, string title, bool privacy)",
                "baseUrl = \"https://api.bilibili.com/x/v3/fav/folder/edit\"");
            StringAssert.Contains(editFavorite, "fid={Uri.EscapeDataString(fid)}");
            StringAssert.Contains(editFavorite, "title={Uri.EscapeDataString(title)}");
            StringAssert.Contains(editFavorite, "privacy={(privacy ? 1 : 0)}");

            var deleteFavorite = AssertSignedPostMethod(
                source,
                "public ApiModel DeleteFavorite(string mediaId)",
                "baseUrl = \"https://api.bilibili.com/x/v3/fav/folder/del\"");
            StringAssert.Contains(deleteFavorite, "media_ids={Uri.EscapeDataString(mediaId)}");

            var removeFavorite = AssertSignedPostMethod(
                source,
                "public ApiModel RemoveFavorite(string mediaId, string avid)",
                "baseUrl = \"https://api.bilibili.com/x/v3/fav/resource/batch-del\"");
            StringAssert.Contains(removeFavorite, "resources={Uri.EscapeDataString(avid + \":2\")}");
            StringAssert.Contains(removeFavorite, "media_id={Uri.EscapeDataString(mediaId)}");
        }

        [TestMethod]
        public void MyCollectPage_ExposesCreateAndManageCommands()
        {
            var xaml = ReadFile("BiliBili.UWP/Pages/User/MyCollectPage.xaml");
            var codeBehind = ReadFile("BiliBili.UWP/Pages/User/MyCollectPage.xaml.cs");

            var secondaryCommands = Between(xaml, "<CommandBar.SecondaryCommands>", "</CommandBar.SecondaryCommands>");
            var createFavoriteButton = ElementContaining(secondaryCommands, "AppBarButton", "x:Name=\"btn_CreateFavorite\"");
            StringAssert.Contains(createFavoriteButton, "Click=\"btn_CreateFavorite_Click\"");
            StringAssert.Contains(createFavoriteButton, "Label=\"新建\"");

            var editFavoriteButton = ElementContaining(secondaryCommands, "AppBarButton", "x:Name=\"btn_EditFavorite\"");
            StringAssert.Contains(editFavoriteButton, "Click=\"btn_EditFavorite_Click\"");
            StringAssert.Contains(editFavoriteButton, "Label=\"编辑\"");

            var deleteFavoriteFolderButton = ElementContaining(secondaryCommands, "AppBarButton", "x:Name=\"btn_DeleteFavoriteFolder\"");
            StringAssert.Contains(deleteFavoriteFolderButton, "Click=\"btn_DeleteFavoriteFolder_Click\"");
            StringAssert.Contains(deleteFavoriteFolderButton, "Label=\"删除\"");
            Assert.IsFalse(
                xaml.Contains("x:Name=\"btn_ManageFavorite\"", StringComparison.Ordinal),
                "收藏夹操作不应再通过独立的管理按钮承载");

            var favoriteEditor = ElementContaining(xaml, "ContentDialog", "x:Name=\"cd_FavoriteEditor\"");
            StringAssert.Contains(favoriteEditor, "Title=\"新建收藏夹\"");
            StringAssert.Contains(favoriteEditor, "PrimaryButtonText=\"确定\"");
            StringAssert.Contains(favoriteEditor, "SecondaryButtonText=\"取消\"");
            StringAssert.Contains(favoriteEditor, "MaxLength=\"20\"");
            StringAssert.Contains(favoriteEditor, "PrimaryButtonClick=\"FavoriteEditor_PrimaryButtonClick\"");
            var favoriteTitleTextBox = ElementContaining(favoriteEditor, "TextBox", "x:Name=\"txt_FavoriteTitle\"");
            StringAssert.Contains(favoriteTitleTextBox, "PlaceholderText=\"输入收藏夹名称\"");
            StringAssert.Contains(favoriteTitleTextBox, "MaxLength=\"20\"");
            var favoritePublicCheckBox = ElementContaining(favoriteEditor, "CheckBox", "x:Name=\"cb_FavoritePublic\"");
            StringAssert.Contains(favoritePublicCheckBox, "<CheckBox");
            StringAssert.Contains(favoritePublicCheckBox, "IsChecked=\"True\"");

            var createFavoriteHandler = MethodBody(
                codeBehind,
                "private async void btn_CreateFavorite_Click(object sender, RoutedEventArgs e)");
            StringAssert.Contains(createFavoriteHandler, "cd_FavoriteEditor");
            StringAssert.Contains(createFavoriteHandler, "_editingFavorite = false");
            StringAssert.Contains(createFavoriteHandler, "txt_FavoriteTitle");
            StringAssert.Contains(createFavoriteHandler, "cd_FavoriteEditor.ShowAsync");

            var editFavoriteHandler = MethodBody(
                codeBehind,
                "private async void btn_EditFavorite_Click(object sender, RoutedEventArgs e)");
            StringAssert.Contains(editFavoriteHandler, "CurrentFavorite");
            StringAssert.Contains(editFavoriteHandler, "cd_FavoriteEditor");
            StringAssert.Contains(editFavoriteHandler, "_editingFavorite = true");
            StringAssert.Contains(editFavoriteHandler, "txt_FavoriteTitle");
            StringAssert.Contains(editFavoriteHandler, "cb_FavoritePublic");
            StringAssert.Contains(editFavoriteHandler, "cd_FavoriteEditor.ShowAsync");

            var favoriteEditorHandler = MethodBody(
                codeBehind,
                "private async void FavoriteEditor_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)");
            StringAssert.Contains(favoriteEditorHandler, "args.Cancel = true");
            StringAssert.Contains(favoriteEditorHandler, "title = txt_FavoriteTitle.Text?.Trim()");
            StringAssert.Contains(favoriteEditorHandler, "string.IsNullOrEmpty(title)");
            StringAssert.Contains(favoriteEditorHandler, "title.Length > 20");
            StringAssert.Contains(favoriteEditorHandler, "EditFavoriteFolder");
            StringAssert.Contains(favoriteEditorHandler, "CreateFavoriteFolder");
            StringAssert.Contains(favoriteEditorHandler, "cd_FavoriteEditor.Hide()");
            var editorSuccessIndex = favoriteEditorHandler.IndexOf("if (success)", StringComparison.Ordinal);
            Assert.IsTrue(editorSuccessIndex >= 0, "编辑器片段缺少成功判断");
            var editorHideIndex = favoriteEditorHandler.IndexOf("cd_FavoriteEditor.Hide()", StringComparison.Ordinal);
            Assert.IsTrue(editorHideIndex > editorSuccessIndex, "编辑器未在成功判断后隐藏对话框");
            var titleValidationIndex = favoriteEditorHandler.IndexOf("string.IsNullOrEmpty(title)", StringComparison.Ordinal);
            Assert.IsTrue(titleValidationIndex >= 0, "编辑器片段缺少标题空值校验");
            var titleLengthIndex = favoriteEditorHandler.IndexOf("title.Length > 20", StringComparison.Ordinal);
            Assert.IsTrue(titleLengthIndex > titleValidationIndex, "编辑器片段缺少标题长度校验");
            var validationReturnIndex = favoriteEditorHandler.IndexOf(
                "return;",
                titleLengthIndex + "title.Length > 20".Length,
                StringComparison.Ordinal);
            Assert.IsTrue(validationReturnIndex > titleLengthIndex, "编辑器标题校验后缺少 return");
            var editViewModelCallIndex = favoriteEditorHandler.IndexOf("EditFavoriteFolder", StringComparison.Ordinal);
            var createViewModelCallIndex = favoriteEditorHandler.IndexOf("CreateFavoriteFolder", StringComparison.Ordinal);
            var firstViewModelCallIndex = FirstIndex(editViewModelCallIndex, createViewModelCallIndex);
            Assert.IsTrue(firstViewModelCallIndex > validationReturnIndex, "标题校验 return 之前调用了 ViewModel");

            var deleteFavoriteFolderHandler = MethodBody(
                codeBehind,
                "private async void btn_DeleteFavoriteFolder_Click(object sender, RoutedEventArgs e)");
            StringAssert.Contains(deleteFavoriteFolderHandler, "MessageDialog");
            StringAssert.Contains(deleteFavoriteFolderHandler, "CurrentFavorite.title");
            StringAssert.Contains(deleteFavoriteFolderHandler, "ShowAsync");
            StringAssert.Contains(deleteFavoriteFolderHandler, "new UICommand(\"确认\")");
            StringAssert.Contains(deleteFavoriteFolderHandler, "new UICommand(\"取消\")");

            const string cancelBranchMarker = "if (command.Label != \"确认\")";
            var cancelBranchIndex = deleteFavoriteFolderHandler.IndexOf(cancelBranchMarker, StringComparison.Ordinal);
            Assert.IsTrue(cancelBranchIndex >= 0, "删除方法缺少 if (command.Label != \"确认\") 分支");
            const string cancelReturnMarker = "return;";
            var cancelReturnIndex = deleteFavoriteFolderHandler.IndexOf(
                cancelReturnMarker,
                cancelBranchIndex + cancelBranchMarker.Length,
                StringComparison.Ordinal);
            Assert.IsTrue(cancelReturnIndex > cancelBranchIndex, "取消确认分支缺少 return;");
            const string deleteFolderCallMarker = "await myFollowVideoVM.DeleteCurrentFavoriteFolder()";
            var deleteFolderCallIndex = deleteFavoriteFolderHandler.IndexOf(
                deleteFolderCallMarker,
                cancelReturnIndex + cancelReturnMarker.Length,
                StringComparison.Ordinal);
            Assert.IsTrue(
                deleteFolderCallIndex > cancelReturnIndex,
                "DeleteCurrentFavoriteFolder 未位于取消分支 return 之后");
        }

        [TestMethod]
        public void MyCollectPage_GatesFolderManagementByLoginAndSelection()
        {
            var source = ReadFile("BiliBili.UWP/Pages/User/MyCollectPage.xaml.cs");

            var createHandler = MethodBody(
                source,
                "private async void btn_CreateFavorite_Click(object sender, RoutedEventArgs e)");
            StringAssert.Contains(createHandler, "!ApiHelper.IsLogin()");
            StringAssert.Contains(createHandler, "await Utils.ShowLoginDialog()");

            var editHandler = MethodBody(
                source,
                "private async void btn_EditFavorite_Click(object sender, RoutedEventArgs e)");
            StringAssert.Contains(editHandler, "!ApiHelper.IsLogin()");
            StringAssert.Contains(editHandler, "await Utils.ShowLoginDialog()");

            var updateState = MethodBody(source, "private void UpdateFavoriteCommandState()");
            StringAssert.Contains(updateState, "btn_EditFavorite.IsEnabled");
            StringAssert.Contains(updateState, "btn_DeleteFavoriteFolder.IsEnabled");
            StringAssert.Contains(updateState, "myFollowVideoVM.CurrentFavorite != null");

            var updateCallCount = 0;
            var searchStart = 0;
            while ((searchStart = source.IndexOf("UpdateFavoriteCommandState();", searchStart, StringComparison.Ordinal)) >= 0)
            {
                updateCallCount++;
                searchStart += "UpdateFavoriteCommandState();".Length;
            }

            Assert.IsTrue(updateCallCount >= 3, "收藏夹管理按钮状态没有在页面生命周期和操作后更新");
        }

        [TestMethod]
        public void MyCollectPage_BatchDeleteCopiesSelectedItemsBeforeRemovingThem()
        {
            var source = ReadFile("BiliBili.UWP/Pages/User/MyCollectPage.xaml.cs");
            var deleteHandler = MethodBody(
                source,
                "private async void btn_Delete_Click(object sender, RoutedEventArgs e)");

            const string snapshotMarker = "var selectedItems = User_ListView_FavouriteVideo.SelectedItems";
            var snapshotIndex = deleteHandler.IndexOf(snapshotMarker, StringComparison.Ordinal);
            Assert.IsTrue(snapshotIndex >= 0, "批量删除前未复制选中项快照");

            var castIndex = deleteHandler.IndexOf(
                ".Cast<FavoriteInfoVideoItemModel>()",
                snapshotIndex + snapshotMarker.Length,
                StringComparison.Ordinal);
            var toListIndex = deleteHandler.IndexOf(
                ".ToList();",
                castIndex + ".Cast<FavoriteInfoVideoItemModel>()".Length,
                StringComparison.Ordinal);
            Assert.IsTrue(castIndex > snapshotIndex, "选中项快照未转换为收藏视频类型序列");
            Assert.IsTrue(toListIndex > castIndex, "选中项快照未复制为列表");

            var foreachIndex = deleteHandler.IndexOf(
                "foreach (var item in selectedItems)",
                toListIndex + ".ToList();".Length,
                StringComparison.Ordinal);
            Assert.IsTrue(foreachIndex > toListIndex, "批量删除未遍历选中项快照");
            Assert.IsFalse(
                deleteHandler.Contains("User_ListView_FavouriteVideo.SelectedItems)", StringComparison.Ordinal),
                "批量删除不应直接遍历会被删除操作改变的 SelectedItems");
        }

        [TestMethod]
        public void ViewModel_ContainsFolderStateTransitions()
        {
            var source = ReadFile("BiliBili.UWP/Modules/User/MyFollowVideoVM.cs");

            var loadFavorite = MethodBody(
                source,
                "public async Task<bool> LoadFavorite(string preferredFid = null, string preferredTitle = null, int preferredIndex = 0)");
            StringAssert.Contains(loadFavorite, "preferredFid");
            StringAssert.Contains(loadFavorite, "preferredTitle");
            StringAssert.Contains(loadFavorite, "preferredIndex");
            StringAssert.Contains(loadFavorite, "fid == preferredFid");
            StringAssert.Contains(loadFavorite, "title == preferredTitle");
            StringAssert.Contains(loadFavorite, "Math.Min(preferredIndex, MyFavorite.Count - 1)");

            var loadFavoriteVideos = MethodBody(
                source,
                "public async Task LoadFavoriteVideos()");
            StringAssert.Contains(loadFavoriteVideos, "CurrentFavorite == null");
            StringAssert.Contains(loadFavoriteVideos, "Videos = null");
            StringAssert.Contains(loadFavoriteVideos, "FavoriteInfo = null");
            var currentFavoriteGuardIndex = loadFavoriteVideos.IndexOf("if (CurrentFavorite == null)", StringComparison.Ordinal);
            Assert.IsTrue(currentFavoriteGuardIndex >= 0, "LoadFavoriteVideos 缺少 CurrentFavorite 为空保护");
            var currentFavoriteGuardReturnIndex = loadFavoriteVideos.IndexOf(
                "return;",
                currentFavoriteGuardIndex + "if (CurrentFavorite == null)".Length,
                StringComparison.Ordinal);
            var currentFavoriteIdIndex = loadFavoriteVideos.IndexOf(
                "CurrentFavorite.id",
                currentFavoriteGuardReturnIndex + "return;".Length,
                StringComparison.Ordinal);
            Assert.IsTrue(currentFavoriteGuardReturnIndex > currentFavoriteGuardIndex, "CurrentFavorite 为空保护缺少提前返回");
            Assert.IsTrue(currentFavoriteIdIndex > currentFavoriteGuardReturnIndex, "CurrentFavorite.id 未位于空值保护之后");

            var createFavoriteFolder = MethodBody(
                source,
                "public async Task<bool> CreateFavoriteFolder(string title, bool privacy)");
            StringAssert.Contains(createFavoriteFolder, "title = title?.Trim()");
            StringAssert.Contains(createFavoriteFolder, "string.IsNullOrEmpty(title)");
            StringAssert.Contains(createFavoriteFolder, "title.Length > 20");
            StringAssert.Contains(createFavoriteFolder, "followAPI.CreateFavorite(title, privacy)");
            StringAssert.Contains(createFavoriteFolder, "preferredFid");
            StringAssert.Contains(createFavoriteFolder, "LoadFavorite(preferredFid, title)");
            AssertLoadingLifecycle(
                createFavoriteFolder,
                "CreateFavoriteFolder",
                "followAPI.CreateFavorite");
            var createSuccessIndex = createFavoriteFolder.IndexOf("if (data.success)", StringComparison.Ordinal);
            var createRefreshIndex = createFavoriteFolder.IndexOf(
                "LoadFavorite(preferredFid, title)",
                createSuccessIndex + "if (data.success)".Length,
                StringComparison.Ordinal);
            Assert.IsTrue(createSuccessIndex >= 0, "CreateFavoriteFolder 缺少成功判断");
            Assert.IsTrue(createRefreshIndex > createSuccessIndex, "CreateFavoriteFolder 未在成功判断后刷新");

            var editFavoriteFolder = MethodBody(
                source,
                "public async Task<bool> EditFavoriteFolder(string title, bool privacy)");
            StringAssert.Contains(editFavoriteFolder, "title = title?.Trim()");
            StringAssert.Contains(editFavoriteFolder, "string.IsNullOrEmpty(title)");
            StringAssert.Contains(editFavoriteFolder, "title.Length > 20");
            StringAssert.Contains(editFavoriteFolder, "followAPI.EditFavorite(CurrentFavorite.fid, title, privacy)");
            StringAssert.Contains(editFavoriteFolder, "LoadFavorite(CurrentFavorite.fid");
            AssertLoadingLifecycle(
                editFavoriteFolder,
                "EditFavoriteFolder",
                "followAPI.EditFavorite");
            var editSuccessIndex = editFavoriteFolder.IndexOf("if (data.success)", StringComparison.Ordinal);
            var editRefreshIndex = editFavoriteFolder.IndexOf(
                "LoadFavorite(CurrentFavorite.fid",
                editSuccessIndex + "if (data.success)".Length,
                StringComparison.Ordinal);
            Assert.IsTrue(editSuccessIndex >= 0, "EditFavoriteFolder 缺少成功判断");
            Assert.IsTrue(editRefreshIndex > editSuccessIndex, "EditFavoriteFolder 未在成功判断后刷新");

            var deleteCurrentFavoriteFolder = MethodBody(
                source,
                "public async Task<bool> DeleteCurrentFavoriteFolder()");
            StringAssert.Contains(deleteCurrentFavoriteFolder, "followAPI.DeleteFavorite(current.id)");
            StringAssert.Contains(deleteCurrentFavoriteFolder, "results.GetJson<ApiDataModel<object>>()");
            StringAssert.Contains(deleteCurrentFavoriteFolder, "MyFavorite.Remove");
            AssertLoadingLifecycle(
                deleteCurrentFavoriteFolder,
                "DeleteCurrentFavoriteFolder",
                "followAPI.DeleteFavorite");

            const string successMarker = "if (data.success)";
            var successIndex = deleteCurrentFavoriteFolder.IndexOf(successMarker, StringComparison.Ordinal);
            Assert.IsTrue(successIndex >= 0, "删除方法缺少成功判断：if (data.success)");
            const string loadFavoriteMarker = "LoadFavorite(null, null, preferredIndex)";
            var loadFavoriteIndex = deleteCurrentFavoriteFolder.IndexOf(
                loadFavoriteMarker,
                successIndex + successMarker.Length,
                StringComparison.Ordinal);
            Assert.IsTrue(loadFavoriteIndex > successIndex, "删除成功后未按 preferredIndex 刷新收藏夹");
            StringAssert.Contains(deleteCurrentFavoriteFolder, "Math.Min(preferredIndex, MyFavorite.Count - 1)");

            const string loadFailureMarker = "if (!loaded)";
            var loadFailureIndex = deleteCurrentFavoriteFolder.IndexOf(
                loadFailureMarker,
                loadFavoriteIndex + loadFavoriteMarker.Length,
                StringComparison.Ordinal);
            Assert.IsTrue(loadFailureIndex > loadFavoriteIndex, "删除方法缺少刷新失败回退分支");

            var removeIndex = deleteCurrentFavoriteFolder.IndexOf(
                "MyFavorite.Remove",
                successIndex + successMarker.Length,
                StringComparison.Ordinal);
            Assert.IsTrue(removeIndex > successIndex, "删除成功判断未先于 MyFavorite.Remove");
            Assert.IsTrue(removeIndex > loadFailureIndex, "刷新失败回退分支未先于 MyFavorite.Remove");

            const string apiFailureMarker = "if (!results.status)";
            var apiFailureIndex = deleteCurrentFavoriteFolder.IndexOf(apiFailureMarker, StringComparison.Ordinal);
            Assert.IsTrue(apiFailureIndex >= 0, "删除方法缺少 API 失败分支");
            var apiFailureReturnIndex = deleteCurrentFavoriteFolder.IndexOf(
                "return false;",
                apiFailureIndex + apiFailureMarker.Length,
                StringComparison.Ordinal);
            Assert.IsTrue(apiFailureReturnIndex > apiFailureIndex, "API 失败分支未返回 false");

            const string businessFailureMarker = "if (!data.success)";
            var businessFailureIndex = deleteCurrentFavoriteFolder.IndexOf(businessFailureMarker, StringComparison.Ordinal);
            Assert.IsTrue(businessFailureIndex >= 0, "删除方法缺少业务失败分支");
            var businessFailureReturnIndex = deleteCurrentFavoriteFolder.IndexOf(
                "return false;",
                businessFailureIndex + businessFailureMarker.Length,
                StringComparison.Ordinal);
            Assert.IsTrue(businessFailureReturnIndex > businessFailureIndex, "业务失败分支未返回 false");
            Assert.IsTrue(apiFailureReturnIndex < removeIndex, "API 失败分支之后仍不应移除收藏夹");
            Assert.IsTrue(businessFailureReturnIndex < removeIndex, "业务失败分支之后仍不应移除收藏夹");

            const string emptyListMarker = "if (MyFavorite.Count == 0)";
            var emptyListIndex = deleteCurrentFavoriteFolder.IndexOf(emptyListMarker, StringComparison.Ordinal);
            Assert.IsTrue(emptyListIndex >= 0, "删除方法缺少空收藏夹列表判断");
            var deleteMethodEndIndex = deleteCurrentFavoriteFolder.Length;
            AssertCleanupAfterEmptyList(deleteCurrentFavoriteFolder, emptyListIndex, deleteMethodEndIndex, "FavoriteInfo = null");
            AssertCleanupAfterEmptyList(deleteCurrentFavoriteFolder, emptyListIndex, deleteMethodEndIndex, "Videos = null");
            AssertCleanupAfterEmptyList(deleteCurrentFavoriteFolder, emptyListIndex, deleteMethodEndIndex, "ShowLoadMore = false");
            AssertCleanupAfterEmptyList(deleteCurrentFavoriteFolder, emptyListIndex, deleteMethodEndIndex, "Nothing = true");
            AssertCleanupAfterEmptyList(deleteCurrentFavoriteFolder, emptyListIndex, deleteMethodEndIndex, "CurrentFavorite = null");

            var removeFavoriteVideo = MethodBody(
                source,
                "public async Task<bool> RemoveFavoriteVideo( FavoriteInfoVideoItemModel item)");
            StringAssert.Contains(removeFavoriteVideo, "followAPI.RemoveFavorite(CurrentFavorite.id, item.id)");
            StringAssert.Contains(removeFavoriteVideo, "Utils.ShowMessageToast(data.message)");
        }

        [TestMethod]
        public void ViewModel_CurrentFavoriteSkipsNotificationForSameReference()
        {
            var source = ReadFile("BiliBili.UWP/Modules/User/MyFollowVideoVM.cs");
            var currentFavorite = MethodBody(
                source,
                "public FavoriteItemModel CurrentFavorite");

            StringAssert.Contains(currentFavorite, "if (ReferenceEquals(_currentFavorite, value))");
            var sameReferenceGuardIndex = currentFavorite.IndexOf(
                "if (ReferenceEquals(_currentFavorite, value))",
                StringComparison.Ordinal);
            var guardReturnIndex = currentFavorite.IndexOf(
                "return;",
                sameReferenceGuardIndex + "if (ReferenceEquals(_currentFavorite, value))".Length,
                StringComparison.Ordinal);
            var notificationIndex = currentFavorite.IndexOf(
                "DoPropertyChanged(\"CurrentFavorite\")",
                StringComparison.Ordinal);

            Assert.IsTrue(guardReturnIndex > sameReferenceGuardIndex, "CurrentFavorite setter 缺少相同实例的提前返回");
            Assert.IsTrue(notificationIndex > guardReturnIndex, "CurrentFavorite setter 未在相同实例保护之后触发通知");
        }

        [TestMethod]
        public void ViewModel_TreatsMissingFavoriteMediasAsEmptyState()
        {
            var source = ReadFile("BiliBili.UWP/Modules/User/MyFollowVideoVM.cs");
            var loadFavoriteVideos = MethodBody(
                source,
                "public async Task LoadFavoriteVideos()");

            StringAssert.Contains(loadFavoriteVideos, "if (data.data == null)");
            StringAssert.Contains(
                loadFavoriteVideos,
                "var medias = data.data.medias ?? new ObservableCollection<FavoriteInfoVideoItemModel>();");
            StringAssert.Contains(loadFavoriteVideos, "Videos = medias;");
            Assert.IsFalse(
                loadFavoriteVideos.Contains("data.data == null || data.data.medias == null", StringComparison.Ordinal),
                "空收藏夹不应被当作收藏内容请求失败");
        }

        private static void AssertCleanupAfterEmptyList(
            string source,
            int emptyListIndex,
            int methodEndIndex,
            string cleanupMarker)
        {
            var cleanupIndex = source.IndexOf(cleanupMarker, emptyListIndex + "if (MyFavorite.Count == 0)".Length, StringComparison.Ordinal);
            Assert.IsTrue(
                cleanupIndex > emptyListIndex && cleanupIndex < methodEndIndex,
                $"空列表判断后缺少清理语句：{cleanupMarker}");
        }

        private static int FirstIndex(int first, int second)
        {
            if (first < 0)
            {
                return second;
            }

            if (second < 0)
            {
                return first;
            }

            return Math.Min(first, second);
        }

        private static void AssertLoadingBeforeRequest(string source, string methodName, string requestMarker)
        {
            var loadingIndex = source.IndexOf("if (Loading)", StringComparison.Ordinal);
            Assert.IsTrue(loadingIndex >= 0, $"{methodName} 片段缺少第一个 if (Loading) 防重复检查");

            var requestIndex = source.IndexOf(requestMarker, StringComparison.Ordinal);
            Assert.IsTrue(requestIndex >= 0, $"{methodName} 片段缺少请求：{requestMarker}");
            Assert.IsTrue(
                loadingIndex < requestIndex,
                $"{methodName} 的第一个 if (Loading) 未位于请求之前");
        }

        private static void AssertLoadingLifecycle(string source, string methodName, string requestMarker)
        {
            AssertLoadingBeforeRequest(source, methodName, requestMarker);

            var loadingTrueIndex = source.IndexOf("Loading = true", StringComparison.Ordinal);
            Assert.IsTrue(loadingTrueIndex >= 0, $"{methodName} 片段缺少 Loading = true");

            var requestIndex = source.IndexOf(requestMarker, loadingTrueIndex, StringComparison.Ordinal);
            Assert.IsTrue(requestIndex > loadingTrueIndex, $"{methodName} 未在设置 Loading 后请求");

            var finallyIndex = source.IndexOf("finally", requestIndex, StringComparison.Ordinal);
            Assert.IsTrue(finallyIndex > requestIndex, $"{methodName} 片段缺少 finally 恢复状态");

            var loadingFalseIndex = source.IndexOf("Loading = false", finallyIndex, StringComparison.Ordinal);
            Assert.IsTrue(loadingFalseIndex > finallyIndex, $"{methodName} finally 未恢复 Loading");
        }

        private static string AssertSignedPostMethod(string source, string methodSignature, string endpoint)
        {
            var methodBody = MethodBody(source, methodSignature);
            StringAssert.Contains(methodBody, endpoint);
            StringAssert.Contains(methodBody, "method = HttpMethod.POST");
            StringAssert.Contains(methodBody, "ApiUtils.MustParameter(ApiUtils.AndroidTVKey");
            StringAssert.Contains(methodBody, "AndroidTVKey");
            StringAssert.Contains(methodBody, "ApiUtils.GetSign(api.body, ApiUtils.AndroidTVKey)");
            return methodBody;
        }

    }
}
