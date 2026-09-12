using BiliBili.UWP.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 互动弹幕类型筛选对话框：勾选要显示的类型，点确定后写入设置。
    /// </summary>
    public static class InteractiveDanmakuTypeDialog
    {
        private static readonly InteractiveDanmakuType[] AllTypes =
        {
            InteractiveDanmakuType.Vote,
            InteractiveDanmakuType.Grade,
            InteractiveDanmakuType.Up,
            InteractiveDanmakuType.Link,
            InteractiveDanmakuType.Attention
        };

        /// <summary>当前选择的可读摘要，用于设置项的入口按钮文案。</summary>
        public static string GetSummary()
        {
            var mask = SettingHelper.Get_InteractiveDanmakuTypes();
            if (mask == 0)
            {
                return "不显示";
            }

            var names = new List<string>();
            foreach (var type in AllTypes)
            {
                if (SettingHelper.Is_InteractiveDanmakuTypeEnabled(mask, type))
                {
                    names.Add(GetTypeName(type));
                }
            }

            return names.Count == AllTypes.Length
                ? "全部"
                : string.Join("、", names);
        }

        /// <summary>弹出类型选择对话框；点确定时保存选择并返回 true，取消返回 false。</summary>
        public static async Task<bool> ShowAsync()
        {
            var mask = SettingHelper.Get_InteractiveDanmakuTypes();
            var panel = new StackPanel();
            var boxes = new List<KeyValuePair<CheckBox, InteractiveDanmakuType>>();
            foreach (var type in AllTypes)
            {
                var box = new CheckBox
                {
                    Content = GetTypeName(type),
                    IsChecked = SettingHelper.Is_InteractiveDanmakuTypeEnabled(mask, type),
                    Margin = new Thickness(0, 4, 0, 0)
                };
                panel.Children.Add(box);
                boxes.Add(new KeyValuePair<CheckBox, InteractiveDanmakuType>(box, type));
            }

            var dialog = new ContentDialog
            {
                Title = "显示互动弹幕",
                Content = panel,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result;
            try
            {
                result = await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("打开互动弹幕类型设置失败", LogType.ERROR, ex);
                return false;
            }

            if (result != ContentDialogResult.Primary)
            {
                return false;
            }

            foreach (var pair in boxes)
            {
                SettingHelper.Set_InteractiveDanmakuTypeEnabled(
                    pair.Value,
                    pair.Key.IsChecked == true);
            }

            return true;
        }

        private static string GetTypeName(InteractiveDanmakuType type)
        {
            switch (type)
            {
                case InteractiveDanmakuType.Vote:
                    return "投票弹幕";
                case InteractiveDanmakuType.Grade:
                    return "评分弹幕";
                case InteractiveDanmakuType.Up:
                    return "UP主头像弹幕";
                case InteractiveDanmakuType.Link:
                    return "关联视频弹幕";
                default:
                    return "关注弹幕";
            }
        }
    }
}
