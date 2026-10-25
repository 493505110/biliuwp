using BiliBili.UWP.Modules.Playback;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Popups;

namespace BiliBili.UWP.Helper
{
    public static class LivePlaybackDialog
    {
        private static readonly SemaphoreSlim promptGate = new SemaphoreSlim(1, 1);

        public static async Task ShowDrmAsync(int roomId, Func<bool> isCurrent)
        {
            if (roomId <= 0 || !isCurrent() || !await promptGate.WaitAsync(0)) return;
            try
            {
                if (!isCurrent()) return;
                var openWeb = new UICommand("打开官方网页");
                var dialog = new MessageDialog(LivePlaybackSources.DrmMessage, "加密直播");
                dialog.Commands.Add(openWeb);
                dialog.Commands.Add(new UICommand("关闭"));
                dialog.DefaultCommandIndex = 0;
                dialog.CancelCommandIndex = 1;
                var selected = await dialog.ShowAsync();
                if (selected == openWeb && isCurrent())
                {
                    if (!await Launcher.LaunchUriAsync(new Uri("https://live.bilibili.com/" + roomId)))
                        Utils.ShowMessageToast("无法打开浏览器，请在浏览器中访问该直播间", 3000);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("显示加密直播提示或打开官方网页失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("无法打开官方网页，请在浏览器中访问该直播间", 3000);
            }
            finally
            {
                promptGate.Release();
            }
        }
    }
}
