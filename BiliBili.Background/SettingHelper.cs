using System;
using BiliBili.UWP.Helper;
using Windows.Storage;

namespace BiliBili.Background
{
    /// <summary>
    /// 后台任务侧的设置读写。与主应用的 BiliBili.UWP.Helper.SettingHelper 是两个独立类，
    /// 靠 csproj 链接进来的 SettingKeys 保证 key 不漂移。
    /// </summary>
    internal static class SettingHelper
    {
        private static ApplicationDataContainer Container
        {
            get { return ApplicationData.Current.LocalSettings; }
        }

        /// <summary>动态磁贴总开关</summary>
        public static bool Get_DTCT()
        {
            return GetBoolOrDefaultTrue(SettingKeys.DTCT);
        }

        /// <summary>投稿视频更新通知</summary>
        public static bool Get_DT()
        {
            return GetBoolOrDefaultTrue(SettingKeys.DT);
        }

        /// <summary>番剧更新通知</summary>
        public static bool Get_FJ()
        {
            return GetBoolOrDefaultTrue(SettingKeys.FJ);
        }

        /// <summary>已通知过的 dynamic_id 逗号串，用于判断哪些是新动态</summary>
        public static string Get_DynamicNotifiedIds()
        {
            return Container.Values[SettingKeys.DynamicNotifiedIds] as string ?? "";
        }

        public static void Set_DynamicNotifiedIds(string value)
        {
            Container.Values[SettingKeys.DynamicNotifiedIds] = value ?? "";
        }

        public static long Get_UserID()
        {
            var value = Container.Values[SettingKeys.UserID];
            return value == null ? 0 : Convert.ToInt64(value);
        }

        //三个通知开关缺省为开，与主应用 SettingHelper 的行为保持一致
        private static bool GetBoolOrDefaultTrue(string key)
        {
            var value = Container.Values[key];
            if (value == null)
            {
                Container.Values[key] = true;
                return true;
            }
            return (bool)value;
        }
    }
}
