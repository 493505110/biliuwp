using System;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 弹幕画布尺寸计算。
    /// 脚本弹幕作品自带的 Akari 库用 maximizeInContainer 把作品舞台等比缩放进弹幕画布
    /// （ratio = min(画布宽/作品宽, 画布高/作品高) 后居中），也就是说画布尺寸直接决定
    /// 作品整体的缩放与位置。因此画布必须等于「视频实际渲染矩形」，而不是整个播放器区域：
    /// 播放器用 Stretch=Uniform 渲染，非 16:9 的视频会在播放器里留有黑边，
    /// 若画布铺满播放器区域，作品就会被整体缩放出错位。
    /// 这里只做纯计算，不依赖任何 UWP API，便于单元测试直接编译。
    /// </summary>
    public static class DanmakuViewport
    {
        /// <summary>
        /// 计算内容按 Stretch=Uniform（等比、完整可见、居中）放进区域后的实际渲染尺寸。
        /// </summary>
        /// <param name="naturalWidth">内容自然宽度（视频轨宽），像素。</param>
        /// <param name="naturalHeight">内容自然高度（视频轨高），像素。</param>
        /// <param name="areaWidth">可用区域宽度（播放器区域宽），像素。</param>
        /// <param name="areaHeight">可用区域高度（播放器区域高），像素。</param>
        /// <param name="width">等比缩放后的渲染宽度。</param>
        /// <param name="height">等比缩放后的渲染高度。</param>
        /// <returns>参数有效时返回 true；任一参数非正数或非有限值（NaN/Infinity）时返回 false。</returns>
        public static bool TryFit(
            double naturalWidth,
            double naturalHeight,
            double areaWidth,
            double areaHeight,
            out double width,
            out double height)
        {
            width = 0;
            height = 0;

            // 自然尺寸在媒体打开前为 0，布局尚未完成时区域尺寸也可能是 0；
            // NaN/Infinity 则可能来自异常的媒体元数据或布局计算，一律当作未知处理，
            // 由调用方保留控件现状而不是把画布清成 0 尺寸。
            if (!IsValidSize(naturalWidth)
                || !IsValidSize(naturalHeight)
                || !IsValidSize(areaWidth)
                || !IsValidSize(areaHeight))
            {
                return false;
            }

            var scale = Math.Min(areaWidth / naturalWidth, areaHeight / naturalHeight);

            width = naturalWidth * scale;
            height = naturalHeight * scale;
            return true;
        }

        /// <summary>尺寸取值有效：有限、且为正数。</summary>
        private static bool IsValidSize(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        }
    }
}
