using System;
using Windows.ApplicationModel.Background;

namespace BiliBili.Background
{
    /// <summary>
    /// 定时后台任务入口，由 Package.appxmanifest 的 windows.backgroundTasks 扩展指向。
    /// 实际逻辑都在 AttentionTileService，主应用前台刷新走的是同一份代码。
    /// </summary>
    public sealed class BackgroundTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            //deferral 必须在任何 await 之前取，否则任务可能在异步操作完成前就被回收
            var deferral = taskInstance.GetDeferral();
            try
            {
                await BgLog.WriteAsync("后台任务触发");
                await AttentionTileService.UpdateCoreAsync();
            }
            catch (Exception ex)
            {
                await BgLog.WriteAsync("后台任务执行失败", ex);
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
