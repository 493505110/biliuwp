using BiliBili.UWP.Api;
using BiliBili.UWP.Api.User;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BiliBili.UWP.Modules
{
    public sealed class UserSpaceFavoritesVM : IModules
    {
        private readonly FollowAPI api = new FollowAPI();
        private string mid;
        private int generation, nextPage = 1;
        private bool active, foldersLoaded, hasMore = true;
        public ObservableCollection<UserSpaceFavoriteFolder> Folders { get; } = new ObservableCollection<UserSpaceFavoriteFolder>();
        public ObservableCollection<UserSpaceVideo> Videos { get; } = new ObservableCollection<UserSpaceVideo>();
        public UserSpaceFavoriteFolder SelectedFolder { get; private set; }
        public string FolderHeader => mid == ApiHelper.GetUserId() ? "我创建的收藏夹" : "TA创建的收藏夹";
        public string Title => SelectedFolder?.Title ?? "收藏";
        public string Summary => SelectedFolder?.Summary ?? "";
        public bool IsLoading { get; private set; }
        public bool HasError { get; private set; }
        public string Status { get; private set; } = "";
        public bool ShowLoadMore => active && !IsLoading && !HasError && SelectedFolder != null && hasMore;
        public bool CanRetry => active && !IsLoading && HasError;
        public bool IsEmpty => foldersLoaded && !IsLoading && !HasError && Folders.Count == 0;
        public bool ShowContent => !IsEmpty;

        public void Activate(string ownerMid)
        {
            active = true;
            if (mid != ownerMid)
            {
                generation++;
                mid = ownerMid;
                Reset();
            }
            NotifyState();
        }

        public void Deactivate()
        {
            active = false;
            generation++;
            IsLoading = false;
            NotifyState();
        }

        private void Reset()
        {
            foldersLoaded = false;
            SelectedFolder = null;
            Folders.Clear();
            Videos.Clear();
            nextPage = 1;
            hasMore = true;
            IsLoading = HasError = false;
            Status = "";
        }

        public Task EnsureLoadedAsync()
        {
            if (!active || string.IsNullOrEmpty(mid)) return Task.CompletedTask;
            if (!foldersLoaded) return LoadFoldersAsync();
            return Videos.Count == 0 && hasMore && !HasError ? LoadMoreAsync() : Task.CompletedTask;
        }

        public async Task RefreshAsync()
        {
            generation++;
            Reset();
            NotifyState();
            await EnsureLoadedAsync();
        }

        public Task RetryAsync() => foldersLoaded ? LoadMoreAsync() : LoadFoldersAsync();

        private async Task LoadFoldersAsync()
        {
            if (!active || IsLoading || string.IsNullOrEmpty(mid)) return;
            int requestGeneration = generation;
            IsLoading = true;
            HasError = false;
            Status = "";
            NotifyState();
            try
            {
                var response = await api.UserCreatedFavorite(mid).Request();
                if (requestGeneration != generation || !active) return;
                if (response == null || !response.status) throw new InvalidOperationException(response?.message ?? "请求失败");
                var folders = UserSpaceFavoriteParser.ParseFoldersResponse(response.GetJObject());
                Folders.Clear();
                foreach (var folder in folders) Folders.Add(folder);
                foldersLoaded = true;
                SelectedFolder = Folders.FirstOrDefault();
                if (SelectedFolder == null)
                {
                    hasMore = false;
                    Status = "该用户没有公开的收藏夹";
                }
            }
            catch (Exception ex)
            {
                if (requestGeneration == generation && active) SetError(ex);
            }
            finally
            {
                if (requestGeneration == generation) { IsLoading = false; NotifyState(); }
            }
            if (requestGeneration == generation && active && SelectedFolder != null && !HasError) await LoadMoreAsync();
        }

        public async Task SelectFolderAsync(UserSpaceFavoriteFolder folder)
        {
            if (!active || !foldersLoaded || folder == null || ReferenceEquals(folder, SelectedFolder)) return;
            generation++;
            SelectedFolder = folder;
            Videos.Clear();
            nextPage = 1;
            hasMore = true;
            IsLoading = HasError = false;
            Status = "";
            NotifyState();
            await LoadMoreAsync();
        }

        public async Task LoadMoreAsync()
        {
            if (!active || IsLoading || SelectedFolder == null || !hasMore) return;
            int requestGeneration = generation;
            long mediaId = SelectedFolder.Id;
            IsLoading = true;
            HasError = false;
            Status = "";
            NotifyState();
            try
            {
                var data = await ReadDataAsync(api.UserFavoriteInfo(mid, mediaId, nextPage));
                if (requestGeneration != generation || !active) return;
                var page = UserSpaceFavoriteParser.ParseVideos(data, nextPage);
                foreach (var video in page.Videos)
                    if (!Videos.Any(existing => existing.Aid == video.Aid)) Videos.Add(video);
                nextPage++;
                hasMore = page.HasMore;
                if (Videos.Count == 0) Status = hasMore ? "本页没有视频，可继续加载" : "收藏夹内没有视频";
            }
            catch (Exception ex)
            {
                if (requestGeneration == generation && active) SetError(ex);
            }
            finally
            {
                if (requestGeneration == generation) { IsLoading = false; NotifyState(); }
            }
        }

        private static async Task<JObject> ReadDataAsync(ApiModel request)
        {
            var response = await request.Request();
            if (response == null || !response.status) throw new InvalidOperationException(response?.message ?? "请求失败");
            var data = await response.GetData<JObject>();
            if (data == null || !data.success) throw new InvalidOperationException(data?.message ?? "服务器响应无法解析");
            return data.data;
        }

        private void SetError(Exception ex)
        {
            LogHelper.WriteLog("读取个人空间收藏失败", LogType.ERROR, ex);
            HasError = true;
            Status = "加载失败，可点击重试：" + ex.Message;
        }

        private void NotifyState()
        {
            foreach (var name in new[] { nameof(SelectedFolder), nameof(FolderHeader), nameof(Title), nameof(Summary), nameof(IsLoading), nameof(HasError), nameof(Status), nameof(ShowLoadMore), nameof(CanRetry), nameof(IsEmpty), nameof(ShowContent) })
                DoPropertyChanged(name);
        }
    }
}
