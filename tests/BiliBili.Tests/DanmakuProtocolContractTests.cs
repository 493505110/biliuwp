using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static BiliBili.Tests.TestRepository;

namespace BiliBili.Tests
{
    [TestClass]
    public class DanmakuProtocolContractTests
    {
        [TestMethod]
        public void ViewMetadataHonorsClosedStateSpecialPackagesAndSupportedModes()
        {
            var service = ReadFile("BiliBili.UWP/Helper/BiliDanmakuService.cs");
            var initial = MethodBody(
                service,
                "private static async Task<BiliDanmakuLoadResult> LoadWebInitialAsync");
            var supplement = MethodBody(
                service,
                "public static async Task<BiliDanmakuLoadResult> LoadSupplementAsync");
            var remainingSegments = MethodBody(
                service,
                "private static async Task<List<SegmentLoadResult>> LoadRemainingSegmentsAsync");
            var specialUrls = MethodBody(service, "private static List<string> GetSpecialDanmakuUrls");
            var location = MethodBody(service, "private static bool TryToLocation");
            var danmakuParser = MethodBody(service, "private static DanmakuModel ParseDanmaku");
            var positionDanmakuValidator = MethodBody(service, "private static bool IsSupportedPositionDanmaku");
            var numberValidator = MethodBody(service, "private static bool IsFinitePositionDanmakuNumber(object value)");
            var player = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");

            StringAssert.Contains(service, "private const long DanmakuClosedState = 1;");
            StringAssert.Contains(initial, "var state = GetDanmakuState(viewResponse.Bytes);");
            StringAssert.Contains(initial, "if (state == DanmakuClosedState)");
            StringAssert.Contains(initial, "var specialDanmakuUrls = GetSpecialDanmakuUrls(viewResponse.Bytes);");
            StringAssert.Contains(specialUrls, "field.Number == 6");
            StringAssert.Contains(remainingSegments, "LoadSpecialPackageWithLimitAsync");
            StringAssert.Contains(supplement, "LoadLegacyAsync(plan.Cid)");
            StringAssert.Contains(location, "case 7:");
            StringAssert.Contains(danmakuParser, "unsupportedDanmakuCount++");
            StringAssert.Contains(danmakuParser, "location == DanmakuLocation.Position && !IsSupportedPositionDanmaku(text)");
            StringAssert.Contains(danmakuParser, "text.Replace(\"/n\", \"\\r\\n\")");
            StringAssert.Contains(danmakuParser, "var displayText = location == DanmakuLocation.Position");
            StringAssert.Contains(positionDanmakuValidator, "var data = JArray.Parse(text);");
            StringAssert.Contains(positionDanmakuValidator, "data.Count > 7 && data.Count < 11");
            StringAssert.Contains(positionDanmakuValidator, "opacity.Length < 2");
            StringAssert.Contains(positionDanmakuValidator, "IsFinitePositionDanmakuNumber(data[10])");
            StringAssert.Contains(numberValidator, "double.IsNaN(number)");
            StringAssert.Contains(player, "load?.IsDanmakuClosed == true");
            StringAssert.Contains(player, "LoadSupplementAsync(initial, cancellationToken)");
        }

        [TestMethod]
        public void CommandDanmakuMapsDocumentedFieldsAndAttentionActions()
        {
            var service = ReadFile("BiliBili.UWP/Helper/InteractiveDanmakuService.cs");
            var parseCommand = MethodBody(service, "private static CommandData ParseCommand");
            var createModel = MethodBody(service, "private static InteractiveDanmakuModel CreateModel");
            var control = ReadFile("BiliBili.UWP/Controls/InteractiveDanmakuControl.xaml.cs");
            var player = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");

            AssertField(parseCommand, 1, "command.Id");
            AssertField(parseCommand, 2, "command.Oid");
            AssertField(parseCommand, 3, "command.Mid");
            AssertField(parseCommand, 4, "command.Command");
            AssertField(parseCommand, 5, "command.Content");
            AssertField(parseCommand, 6, "command.Progress");
            AssertField(parseCommand, 9, "command.Extra");
            AssertField(parseCommand, 10, "command.IdStr");

            StringAssert.Contains(createModel, "normalizedCommand == \"#UP#\"");
            StringAssert.Contains(createModel, "normalizedCommand == \"#LINK#\"");
            StringAssert.Contains(createModel, "normalizedCommand == \"#ATTENTION#\"");
            StringAssert.Contains(createModel, "item.IconUrl = GetString(extra[\"icon\"]);");
            StringAssert.Contains(createModel, "item.RelatedAid = GetLong(extra[\"aid\"]);");
            StringAssert.Contains(createModel, "item.RelatedBvid = GetString(extra[\"bvid\"])");
            StringAssert.Contains(createModel, "Duration = GetDuration(extra)");
            StringAssert.Contains(createModel, "item.PositionX = extra[\"posX\"]");
            StringAssert.Contains(createModel, "item.PositionY = extra[\"posY\"]");
            StringAssert.Contains(createModel, "attentionType >= 0 && attentionType <= 2");
            StringAssert.Contains(control, "InteractiveDanmakuActionKind.Triple");
            StringAssert.Contains(control, "item.AttentionType != 1");
            StringAssert.Contains(control, "item.AttentionType != 0");
            StringAssert.Contains(player, "case InteractiveDanmakuActionKind.Triple:");
            StringAssert.Contains(player, "new VideoAPI().Triple(videoAid).Request()");
            StringAssert.Contains(player, "IsCurrentInteractiveDanmakuItem(item, playbackItem)");
        }

        [TestMethod]
        public void ReverseDanmakuModeIsMappedAndRendered()
        {
            var service = ReadFile("BiliBili.UWP/Helper/BiliDanmakuService.cs");
            var model = ReadFile("Libraries/NSDanmaku-Fork/NSDanmaku/Model/DanmakuModel.cs");
            var legacyParser = ReadFile("Libraries/NSDanmaku-Fork/NSDanmaku/Helper/DanmakuParse.cs");
            var winUiParser = ReadFile("Libraries/NSDanmaku-Fork/NSDanmaku.WinUI/Helper/DanmakuParse.cs");
            var tantanParser = ReadFile("Libraries/NSDanmaku-Fork/NSDanmaku/Helper/TanTanPlay.cs");
            var control = ReadFile("Libraries/NSDanmaku-Fork/NSDanmaku/Controls/Danmaku.xaml.cs");
            var refreshRowHeights = MethodBody(control, "private void RefreshRowHeights(Grid container)");
            var setRowHeight = MethodBody(control, "private void SetRowHeight(Grid container, int row)");
            var ensureRowsForItem = MethodBody(control, "private void EnsureRowsForItem(Grid container, Grid item)");
            var addHorizontalScroll = MethodBody(control, "private async Task AddHorizontalScrollDanmu(");
            var scrollPoolSelection = MethodBody(control, "private bool TryGetScrollPoolPosition(");
            var winUiControl = ReadFile("Libraries/NSDanmaku-Fork/NSDanmaku.WinUI/Controls/Danmaku.xaml.cs");
            var player = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");

            StringAssert.Contains(model, "ReverseScroll");
            StringAssert.Contains(service, "case 6:");
            StringAssert.Contains(service, "location = DanmakuLocation.ReverseScroll;");
            StringAssert.Contains(service, "item.location == DanmakuLocation.ReverseScroll");
            StringAssert.Contains(legacyParser, "case \"6\":");
            StringAssert.Contains(winUiParser, "case \"6\":");
            StringAssert.Contains(control, "AddReverseScrollDanmu");
            StringAssert.Contains(control, "var fromX = GetScrollInitialX(reverse, viewportWidth, itemWidth);");
            StringAssert.Contains(control, "private bool IsScrollPoolAvailable(");
            StringAssert.Contains(control, "var itemX = viewportWidth;");
            StringAssert.Contains(control, "GetScrollEnd(occupied, viewportWidth) > GetScrollMiddle(item, viewportWidth)");
            StringAssert.Contains(winUiControl, "AddReverseScrollDanmu");
            StringAssert.Contains(winUiControl, "reverse ? -grid.ActualWidth : mainContainer.ActualWidth");
            StringAssert.Contains(winUiControl, "GetScrollAvailableRow(Grid item, bool reverse = false)");
            StringAssert.Contains(winUiControl, "lastModel.location == DanmakuLocation.ReverseScroll");
            StringAssert.Contains(player, "case NSDanmaku.Model.DanmakuLocation.ReverseScroll:");
            StringAssert.Contains(player, "danmu.AddReverseScrollDanmu(item, false);");
            StringAssert.Contains(legacyParser, "danmakuText = danmakuText.Replace(\"/n\", \"\\r\\n\");");
            StringAssert.Contains(winUiParser, "danmakuText = danmakuText.Replace(\"/n\", \"\\r\\n\");");
            StringAssert.Contains(tantanParser, "location != DanmakuLocation.Position && danmakuText != null");
            StringAssert.Contains(tantanParser, "danmakuText = danmakuText.Replace(\"/n\", \"\\r\\n\");");
            StringAssert.Contains(refreshRowHeights, "measuredRowHeights[grid] = MeasureDanmakuHeight(grid);");
            StringAssert.Contains(setRowHeight, "var rowHeight = 0.0;");
            StringAssert.Contains(ensureRowsForItem, "measuredRowHeights[item] = MeasureDanmakuHeight(item);");
            StringAssert.Contains(addHorizontalScroll, "grid_Scroll.Children.Add(grid);");
            StringAssert.Contains(addHorizontalScroll, "SetRowHeight(grid_Scroll, 0);");
            StringAssert.Contains(scrollPoolSelection, "logicalY = occupied.LogicalY + occupied.Height + PoolGap;");
        }

        [TestMethod]
        public void OfficialDanmakuDefaultsAndPlacementAreRetained()
        {
            var control = ReadFile("Libraries/NSDanmaku-Fork/NSDanmaku/Controls/Danmaku.xaml.cs");
            var settings = ReadFile("BiliBili.UWP/Helper/SettingHelper.cs");
            var player = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");

            StringAssert.Contains(control, "DanmakuBold = true;");
            StringAssert.Contains(control, "DanmakuFontFamily = \"黑体\";");
            StringAssert.Contains(control, "new PropertyMetadata(5, OnDanmakuDurationChanged)");
            StringAssert.Contains(control, "private const double FixedDanmakuDuration = 3.5;");
            StringAssert.Contains(control, "private const double OfficialDanmakuWidth = 543.0;");
            StringAssert.Contains(control, "private double GetScrollSpeed(double itemWidth)");
            StringAssert.Contains(control, "return (OfficialDanmakuWidth + itemWidth) / GetScrollReferenceDuration();");
            StringAssert.Contains(control, "private double GetScrollDuration(double viewportWidth, double itemWidth)");
            StringAssert.Contains(control, "return (viewportWidth + itemWidth) / GetScrollSpeed(itemWidth);");
            StringAssert.Contains(control, "GetScrollDuration(viewportWidth, itemWidth)");
            StringAssert.Contains(control, "myDoubleAnimationX.To = -itemWidth;//到达");
            StringAssert.Contains(control, "private bool TryReserveFixedSpace(");
            StringAssert.Contains(control, "private bool TryReserveScrollSpace(");
            StringAssert.Contains(control, "private readonly List<List<DanmakuSpaceItem>> topPools");
            StringAssert.Contains(control, "private readonly List<List<DanmakuSpaceItem>> bottomPools");
            StringAssert.Contains(control, "private readonly List<List<DanmakuSpaceItem>> scrollPools");
            StringAssert.Contains(control, "private readonly List<List<DanmakuSpaceItem>> reverseScrollPools");
            StringAssert.Contains(control, "space.Pool = pool;");
            StringAssert.Contains(control, "grid_Top.Children.Add(grid);");
            StringAssert.Contains(control, "SetRowHeight(grid_Top, 0);");
            StringAssert.Contains(control, "grid_Bottom.Children.Add(grid);");
            StringAssert.Contains(control, "SetRowHeight(grid_Bottom, 0);");
            StringAssert.Contains(settings, "Set_DanmuFont(\"黑体\")");
            StringAssert.Contains(settings, "container.Values[\"DMSpeed\"] = 5;");
            StringAssert.Contains(player, "danmu.DanmakuFontFamily = danmuFont;");
            StringAssert.Contains(player, "danmu.DanmakuDuration = Math.Max(1, Convert.ToInt32(danmuSpeed));");
        }

        [TestMethod]
        public void FixedDanmakuLifetimeMatchesOfficialTimerContract()
        {
            var control = ReadFile("Libraries/NSDanmaku-Fork/NSDanmaku/Controls/Danmaku.xaml.cs");
            var lifetime = MethodBody(control, "private sealed class FixedDanmakuLifetime");
            var top = MethodBody(control, "public async Task AddTopDanmu");
            var bottom = MethodBody(control, "public async Task AddBottomDanmu");
            var remove = MethodBody(control, "private void RemoveFixedDanmaku");
            var clear = MethodBody(control, "public void ClearAll");

            StringAssert.Contains(control, "private const int FixedDanmakuTickMilliseconds = 350;");
            StringAssert.Contains(control, "private const int FixedDanmakuTickCount = 10;");
            StringAssert.Contains(lifetime, "Interval = TimeSpan.FromMilliseconds(FixedDanmakuTickMilliseconds)");
            StringAssert.Contains(lifetime, "timer.Tick += OnTick;");
            StringAssert.Contains(lifetime, "timer.Tick -= OnTick;");
            StringAssert.Contains(lifetime, "tickCount++;");
            StringAssert.Contains(lifetime, "if (tickCount < FixedDanmakuTickCount)");
            StringAssert.Contains(lifetime, "completed?.Invoke();");
            StringAssert.Contains(top, "new FixedDanmakuLifetime(() =>");
            StringAssert.Contains(top, "RemoveFixedDanmaku(grid_Top, grid);");
            StringAssert.Contains(bottom, "new FixedDanmakuLifetime(() =>");
            StringAssert.Contains(bottom, "RemoveFixedDanmaku(grid_Bottom, grid);");
            StringAssert.Contains(remove, "lifetime.Stop();");
            StringAssert.Contains(remove, "element.Children.Clear();");
            StringAssert.Contains(clear, "foreach (var item in positionStoryList.ToList())");
            StringAssert.Contains(clear, "positionStoryList.Clear();");
            StringAssert.Contains(clear, "fixedLifetimes.Clear();");
        }

        private static void AssertField(string source, int fieldNumber, string assignment)
        {
            var marker = "case " + fieldNumber + ":";
            var start = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "缺少 protobuf 字段 " + fieldNumber);
            var end = source.IndexOf("break;", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start, "protobuf 字段 " + fieldNumber + " 缺少结束位置");
            StringAssert.Contains(source.Substring(start, end - start), assignment);
        }
    }
}
