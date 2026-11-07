using System;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace BiliBili.UWP.Helper
{
    internal static class WindowThemeHelper
    {
        internal static ResourceDictionary GetThemeResources(string themeName)
        {
            var dictionaries = Application.Current.Resources.ThemeDictionaries;
            if (themeName == "Dark")
            {
                return (ResourceDictionary)dictionaries["Dark"];
            }

            var light = (ResourceDictionary)dictionaries["Light"];
            var palette = new ResourceDictionary { Source = new Uri($"ms-appx:///Theme/{themeName}Theme.xaml") };
            // 已显示的控件持有这些画刷。原地更新颜色，浅色换肤就无需来回切换 RequestedTheme。
            foreach (var entry in palette)
            {
                var brush = (SolidColorBrush)light[entry.Key];
                var color = ((SolidColorBrush)entry.Value).Color;
                if (brush.Color != color)
                {
                    brush.Color = color;
                }
            }
            return light;
        }

        internal static void ApplyTitleBar(ResourceDictionary theme)
        {
            var background = ((SolidColorBrush)theme["Bili-ForeColor"]).Color;
            var buttonHighlight = ((SolidColorBrush)theme["Bili-ForeColor-Dark"]).Color;
            ApplyTitleBar(background, buttonHighlight, Color.FromArgb(255, 254, 254, 254));
        }

        internal static void ApplyTitleBar(Color background, Color buttonHighlight, Color buttonForeground)
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            // 系统随底色选择标题字色。强制近白色会让红切粉时的黑字再跳回白色。
            if (titleBar.ForegroundColor.HasValue)
            {
                titleBar.ForegroundColor = null;
            }
            if (titleBar.InactiveForegroundColor.HasValue)
            {
                titleBar.InactiveForegroundColor = null;
            }
            SetColorIfChanged(titleBar.BackgroundColor, background, color => titleBar.BackgroundColor = color);
            SetColorIfChanged(titleBar.InactiveBackgroundColor, background, color => titleBar.InactiveBackgroundColor = color);
            SetColorIfChanged(titleBar.ButtonBackgroundColor, background, color => titleBar.ButtonBackgroundColor = color);
            SetColorIfChanged(titleBar.ButtonForegroundColor, buttonForeground, color => titleBar.ButtonForegroundColor = color);
            SetColorIfChanged(titleBar.ButtonInactiveBackgroundColor, background, color => titleBar.ButtonInactiveBackgroundColor = color);
            SetColorIfChanged(titleBar.ButtonInactiveForegroundColor, buttonForeground, color => titleBar.ButtonInactiveForegroundColor = color);
            SetColorIfChanged(titleBar.ButtonHoverBackgroundColor, buttonHighlight, color => titleBar.ButtonHoverBackgroundColor = color);
            SetColorIfChanged(titleBar.ButtonHoverForegroundColor, buttonForeground, color => titleBar.ButtonHoverForegroundColor = color);
            SetColorIfChanged(titleBar.ButtonPressedBackgroundColor, buttonHighlight, color => titleBar.ButtonPressedBackgroundColor = color);
            SetColorIfChanged(titleBar.ButtonPressedForegroundColor, buttonForeground, color => titleBar.ButtonPressedForegroundColor = color);
        }

        private static void SetColorIfChanged(Color? current, Color desired, Action<Color> setColor)
        {
            if (current != desired)
            {
                setColor(desired);
            }
        }
    }
}
