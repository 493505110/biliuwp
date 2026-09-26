using NSDanmaku.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 弹幕位置类型筛选对话框：勾选要显示的位置类型，点确定后写入设置。
    /// </summary>
    public static class DanmakuLocationTypeDialog
    {
        // 可在界面上勾选的位置类型。DanmakuLocation.Other 不进界面，但掩码里的位会保留，
        // 避免用户改动选项后把未展示的位清掉。
        private static readonly DanmakuLocation[] AllTypes =
        {
            DanmakuLocation.Scroll,
            DanmakuLocation.Top,
            DanmakuLocation.Bottom,
            DanmakuLocation.ReverseScroll,
            DanmakuLocation.Position
        };

        /// <summary>BAS 弹幕名称：它不属 DanmakuLocation 枚举，由 WebView2 独立渲染，单列一项。</summary>
        private const string BasTypeName = "BAS弹幕";

        /// <summary>当前选择的可读摘要，用于设置页入口按钮文案。</summary>
        public static string GetSummary()
        {
            var names = GetEnabledTypeNames();
            if (names.Count == 0)
            {
                return "不显示";
            }

            return names.Count == AllTypes.Length + 1
                ? "全部"
                : string.Join("、", names);
        }

        /// <summary>播放器设置面板用的短摘要：面板宽度有限，完整名单会压住左侧标签，只显示已选数量。</summary>
        public static string GetShortSummary()
        {
            var names = GetEnabledTypeNames();
            if (names.Count == 0)
            {
                return "不显示";
            }

            return names.Count == AllTypes.Length + 1
                ? "全部"
                : $"已选 {names.Count} 项";
        }

        /// <summary>按固定顺序列出当前开启的类型；BAS 排在最后，与对话框里的勾选项顺序一致。</summary>
        private static List<string> GetEnabledTypeNames()
        {
            var mask = SettingHelper.Get_DanmakuLocationTypes();
            var names = new List<string>();
            foreach (var type in AllTypes)
            {
                if (SettingHelper.Is_DanmakuLocationTypeEnabled(mask, type))
                {
                    names.Add(GetTypeName(type));
                }
            }

            if (SettingHelper.Get_BasDanmakuEnabled())
            {
                names.Add(BasTypeName);
            }

            return names;
        }

        /// <summary>弹出类型选择对话框；点确定时保存选择并返回 true，取消返回 false。</summary>
        public static async Task<bool> ShowAsync()
        {
            var mask = SettingHelper.Get_DanmakuLocationTypes();
            var panel = new StackPanel();
            var boxes = new List<KeyValuePair<CheckBox, DanmakuLocation>>();
            foreach (var type in AllTypes)
            {
                var box = new CheckBox
                {
                    Content = GetTypeName(type),
                    IsChecked = SettingHelper.Is_DanmakuLocationTypeEnabled(mask, type),
                    Margin = new Thickness(0, 4, 0, 0)
                };
                panel.Children.Add(box);
                boxes.Add(new KeyValuePair<CheckBox, DanmakuLocation>(box, type));
            }

            var basBox = new CheckBox
            {
                Content = BasTypeName,
                IsChecked = SettingHelper.Get_BasDanmakuEnabled(),
                Margin = new Thickness(0, 4, 0, 0)
            };
            panel.Children.Add(basBox);

            var dialog = new ContentDialog
            {
                Title = "弹幕显示",
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
                LogHelper.WriteLog("打开弹幕类型过滤设置失败", LogType.ERROR, ex);
                return false;
            }

            if (result != ContentDialogResult.Primary)
            {
                return false;
            }

            foreach (var pair in boxes)
            {
                SettingHelper.Set_DanmakuLocationTypeEnabled(
                    pair.Value,
                    pair.Key.IsChecked == true);
            }

            SettingHelper.Set_BasDanmakuEnabled(basBox.IsChecked == true);

            return true;
        }

        private static string GetTypeName(DanmakuLocation type)
        {
            switch (type)
            {
                case DanmakuLocation.Top:
                    return "顶部弹幕";
                case DanmakuLocation.Bottom:
                    return "底部弹幕";
                case DanmakuLocation.ReverseScroll:
                    return "逆向弹幕";
                case DanmakuLocation.Position:
                    return "高级弹幕";
                default:
                    return "滚动弹幕";
            }
        }
    }
}
