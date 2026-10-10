using System;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 内容等比适配区域的尺寸计算。
    /// M8 舞台铺满播放器区域，不使用这里的视频内容矩形限制画布。
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
