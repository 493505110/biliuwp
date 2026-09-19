using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace BiliBili.Background
{
    /// <summary>
    /// 后台任务无法断点调试，只能靠落盘日志排查。
    /// 主应用用的是 NLog，这里为了不给后台任务引入额外依赖，单独做一份极简实现。
    /// </summary>
    internal static class BgLog
    {
        public static async Task WriteAsync(string message, Exception ex = null)
        {
            try
            {
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + message;
                if (ex != null)
                {
                    line += "|" + ex.GetType().Name + ": " + ex.Message;
                }
                var folder = await ApplicationData.Current.LocalFolder
                    .CreateFolderAsync("log", CreationCollisionOption.OpenIfExists);
                var file = await folder.CreateFileAsync(
                    "background-" + DateTime.Now.ToString("yyyyMMdd") + ".log",
                    CreationCollisionOption.OpenIfExists);
                await FileIO.AppendTextAsync(file, line + Environment.NewLine);
            }
            catch (Exception)
            {
                //日志写入失败不能影响主流程
            }
        }
    }
}
