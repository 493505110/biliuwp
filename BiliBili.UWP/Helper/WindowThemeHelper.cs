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
            var foreground = Color.FromArgb(255, 254, 254, 254);
            ApplyTitleBar(background, buttonHighlight, foreground, foreground);
        }

        internal static void ApplyTitleBar(Color background, Color buttonHighlight, Color foreground, Color buttonForeground)
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = background;
            titleBar.ButtonHoverBackgroundColor = buttonHighlight;
            titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonForegroundColor = buttonForeground;
            titleBar.InactiveBackgroundColor = background;
            titleBar.ButtonInactiveBackgroundColor = background;
            // 背景更新完成后重置并重新应用标题字色，确保换肤时相同的前景色也能刷新。
            titleBar.ForegroundColor = null;
            titleBar.ForegroundColor = foreground;
        }
    }
}
