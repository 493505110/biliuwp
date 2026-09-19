using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.UI.Notifications;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 动态磁贴后台任务的注册与注销。启动页和设置页共用，免得两处逻辑走偏。
    /// </summary>
    internal static class BackgroundTaskRegistrar
    {
        //注册名，对应 Package.appxmanifest 中 windows.backgroundTasks 扩展声明的任务
        private const string TaskName = "BackgroundTask";

        /// <summary>
        /// 按 DTCT 开关同步后台任务的注册状态，返回当前有效的注册（未注册时为 null）。
        /// </summary>
        public static async Task<IBackgroundTaskRegistration> SyncAsync()
        {
            try
            {
                var existing = BackgroundTaskRegistration.AllTasks.Values
                    .FirstOrDefault(x => x.Name == TaskName);

                //总开关关掉时连任务一起注销，否则它每 15 分钟被唤醒一次却什么都不做
                if (!SettingHelper.Get_DTCT())
                {
                    if (existing != null)
                    {
                        existing.Unregister(true);
                    }
                    TileUpdateManager.CreateTileUpdaterForApplication().Clear();
                    return null;
                }

                //已经注册过就别重来一遍，重新注册会把 15 分钟的计时归零
                if (existing != null)
                {
                    return existing;
                }

                //UWP 要求注册前先申请后台执行权限，
                //跳过这一步 Register() 会直接抛 UnauthorizedAccessException
                var access = await BackgroundExecutionManager.RequestAccessAsync();
                if (access == BackgroundAccessStatus.DeniedBySystemPolicy
                    || access == BackgroundAccessStatus.DeniedByUser
                    || access == BackgroundAccessStatus.Unspecified)
                {
                    LogHelper.WriteLog("后台任务权限被拒绝：" + access, LogType.INFO);
                    return null;
                }

                var builder = new BackgroundTaskBuilder()
                {
                    Name = TaskName,
                    TaskEntryPoint = typeof(BiliBili.Background.BackgroundTask).FullName,
                    IsNetworkRequested = true
                };
                //第二个参数是 oneShot，必须为 false 才会每 15 分钟重复触发
                builder.SetTrigger(new TimeTrigger(15, false));
                builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                return builder.Register();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("同步后台任务注册状态失败", LogType.ERROR, ex);
                return null;
            }
        }

        /// <summary>
        /// 后台任务最快也要 15 分钟才触发一次，需要即时反馈的地方先在前台刷一遍磁贴。
        /// </summary>
        public static async Task RefreshTileAsync()
        {
            try
            {
                await new BiliBili.Background.AttentionTileService().UpdateAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("刷新动态磁贴失败", LogType.ERROR, ex);
            }
        }
    }
}
