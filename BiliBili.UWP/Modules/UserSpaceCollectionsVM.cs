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
    public sealed class UserSpaceCollectionsVM : IModules
    {
        private readonly UserCenterAPI api = new UserCenterAPI();
        private string mid;
        private int generation, collectionPage = 1, videoPage = 1;
        private bool active, collectionHasMore = true, videoHasMore = true;
        public ObservableCollection<UserSpaceCollection> Collections { get; } = new ObservableCollection<UserSpaceCollection>();
        public ObservableCollection<UserSpaceVideo> Videos { get; } = new ObservableCollection<UserSpaceVideo>();
        public UserSpaceCollection SelectedCollection { get; private set; }
        public bool ShowCollections => SelectedCollection == null;
        public bool ShowVideos => SelectedCollection != null;
        public bool ShowEmptyCollections => ShowCollections && !IsLoading && !collectionHasMore && Collections.Count == 0;
        public bool ShowCollectionList => ShowCollections && !ShowEmptyCollections;
        public string Title => SelectedCollection?.Title ?? "合集与系列";
        public bool IsLoading { get; private set; }
        public bool CanLoadMore => active && !IsLoading && (ShowCollections ? collectionHasMore : videoHasMore);
        public string Status { get; private set; } = "";

        public void Activate(string ownerMid)
        {
            active = true;
            if (mid == ownerMid) { NotifyState(); return; }
            generation++;
            mid = ownerMid;
            SelectedCollection = null;
            Collections.Clear();
            Videos.Clear();
            collectionPage = videoPage = 1;
            collectionHasMore = videoHasMore = true;
            IsLoading = false;
            Status = "";
            NotifyState();
        }

        public void Deactivate()
        {
            active = false;
            generation++;
            IsLoading = false;
            NotifyState();
        }

        public async Task OpenAsync(UserSpaceCollection collection)
        {
            generation++;
            SelectedCollection = collection;
            Videos.Clear();
            videoPage = 1;
            videoHasMore = true;
            IsLoading = false;
            Status = "";
            NotifyState();
            await LoadMoreAsync();
        }

        public void BackToCollections()
        {
            generation++;
            SelectedCollection = null;
            IsLoading = false;
            Status = Collections.Count == 0 && !collectionHasMore ? "空空如也" : "";
            NotifyState();
        }

        public Task EnsureLoadedAsync()
        {
            return (ShowCollections ? Collections.Count == 0 : Videos.Count == 0)
                ? LoadMoreAsync() : Task.CompletedTask;
        }

        public async Task LoadMoreAsync()
        {
            if (!CanLoadMore || string.IsNullOrEmpty(mid)) return;
            int requestGeneration = generation;
            var selected = SelectedCollection;
            int page = selected == null ? collectionPage : videoPage;
            int pageSize = selected == null ? 20 : 30;
            IsLoading = true;
            Status = "";
            NotifyState();
            try
            {
                var request = selected == null ? api.UserCollections(mid, page, pageSize)
                    : api.UserCollectionVideos(mid, selected.Id, selected.Kind == UserSpaceCollectionKind.Season, page, pageSize);
                var response = await request.Request();
                if (requestGeneration != generation || !active) return;
                if (!response.status) throw new InvalidOperationException(response.message);
                var data = await response.GetData<JObject>();
                if (requestGeneration != generation || !active) return;
                if (data == null || !data.success) throw new InvalidOperationException(data?.message ?? "服务器响应无法解析");
                if (selected == null)
                {
                    var result = UserSpaceCollectionParser.ParseCollections(data.data, page, pageSize);
                    foreach (var item in result.Items)
                        if (!Collections.Any(existing => existing.Id == item.Id && existing.Kind == item.Kind)) Collections.Add(item);
                    collectionPage++;
                    collectionHasMore = result.HasMore;
                    if (Collections.Count == 0) Status = "空空如也";
                }
                else
                {
                    var result = UserSpaceCollectionParser.ParseVideos(data.data, page, pageSize);
                    foreach (var item in result.Items)
                        if (!Videos.Any(existing => existing.Aid == item.Aid)) Videos.Add(item);
                    videoPage++;
                    videoHasMore = result.HasMore;
                    if (Videos.Count == 0) Status = "暂无可播放的视频";
                }
            }
            catch (Exception ex)
            {
                if (requestGeneration != generation || !active) return;
                LogHelper.WriteLog("读取个人空间合集或系列失败", LogType.ERROR, ex);
                Status = "加载失败，可点击“加载更多”重试：" + ex.Message;
            }
            finally
            {
                if (requestGeneration == generation)
                {
                    IsLoading = false;
                    NotifyState();
                }
            }
        }

        private void NotifyState()
        {
            foreach (var name in new[] { nameof(SelectedCollection), nameof(ShowCollections), nameof(ShowVideos), nameof(ShowEmptyCollections), nameof(ShowCollectionList), nameof(Title), nameof(IsLoading), nameof(CanLoadMore), nameof(Status) })
                DoPropertyChanged(name);
        }
    }
}
