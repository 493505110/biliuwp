using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.UI.Notifications;

namespace BiliBili.Background
{
    /// <summary>
    /// 关注动态 → 动态磁贴 + 更新 Toast 的编排。
    /// 后台任务每 15 分钟跑一次；主应用在启动时和打开磁贴开关时也会主动调 UpdateAsync 刷一次，
    /// 否则用户要等满一个触发周期才看得到变化。
    /// </summary>
    public sealed class AttentionTileService
    {
        //磁贴通知队列最多保留 5 条
        private const int MaxTileCount = 5;

        /// <summary>供主应用在前台主动刷新一次。WinRT 不接受 Task，必须导出 IAsyncAction。</summary>
        public IAsyncAction UpdateAsync()
        {
            return UpdateCoreAsync().AsAsyncAction();
        }

        internal static async Task UpdateCoreAsync()
        {
            try
            {
                if (!SettingHelper.Get_DTCT())
                {
                    TileUpdateManager.CreateTileUpdaterForApplication().Clear();
                    await BgLog.WriteAsync("动态磁贴开关已关闭，清空磁贴");
                    return;
                }

                var items = await DynamicFeedApi.GetAttentionUpdateAsync();
                if (items == null || items.Count == 0)
                {
                    await BgLog.WriteAsync("本次没有拿到可用的关注动态");
                    return;
                }

                //先发 Toast 再写回记录：判断"哪些是新的"依赖的正是上一次写下的集合
                ShowToasts(items);
                UpdateTiles(items);
                SettingHelper.Set_DynamicNotifiedIds(string.Join(",", items.Select(x => x.DynamicId)));
                await BgLog.WriteAsync("更新完成，共 " + items.Count + " 条动态");
            }
            catch (Exception ex)
            {
                await BgLog.WriteAsync("更新动态磁贴失败", ex);
            }
        }

        private static void UpdateTiles(List<FeedItem> items)
        {
            var updater = TileUpdateManager.CreateTileUpdaterForApplication();
            updater.EnableNotificationQueue(true);
            updater.Clear();
            foreach (var item in items.Take(MaxTileCount))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(NotificationBuilder.BuildTileXml(item));
                    updater.Update(new TileNotification(doc));
                }
                catch (Exception)
                {
                    //单条磁贴构造失败不影响其余几条
                }
            }
        }

        private static void ShowToasts(List<FeedItem> items)
        {
            var known = SettingHelper.Get_DynamicNotifiedIds()
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (known.Length == 0)
            {
                //首次运行(以及从旧版本升级上来)时把当前这批全部视为已知，
                //否则会一次性弹出一屏通知
                return;
            }

            var knownIds = new HashSet<string>(known);
            var showVideo = SettingHelper.Get_DT();
            var showBangumi = SettingHelper.Get_FJ();
            var notifier = ToastNotificationManager.CreateToastNotifier();
            foreach (var item in items)
            {
                if (knownIds.Contains(item.DynamicId))
                {
                    continue;
                }
                if (item.IsBangumi ? !showBangumi : !showVideo)
                {
                    continue;
                }
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(NotificationBuilder.BuildToastXml(item));
                    notifier.Show(new ToastNotification(doc));
                }
                catch (Exception)
                {
                    //单条通知失败不影响其余
                }
            }
        }
    }
}
