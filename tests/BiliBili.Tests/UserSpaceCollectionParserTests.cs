using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace BiliBili.Tests
{
    [TestClass]
    public class UserSpaceCollectionParserTests
    {
        private static JObject Data(string fixture) =>
            (JObject)JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture + ".json")))["data"];

        [TestMethod]
        public void CapturedSeasons_UseMetadataAndTotalInsteadOfPreviewArchiveCount()
        {
            var page = UserSpaceCollectionParser.ParseCollections(Data("UserSpaceSeasons"), 1, 2);
            Assert.AreEqual(2, page.Items.Count);
            Assert.IsTrue(page.HasMore);
            Assert.AreEqual(UserSpaceCollectionKind.Season, page.Items[0].Kind);
            Assert.AreEqual(3692372L, page.Items[0].Id);
            Assert.AreEqual(8, page.Items[0].Count);
            StringAssert.StartsWith(page.Items[0].Cover, "https://");
            Assert.AreEqual("合集·史图馆全球文明史", page.Items[0].Heading);
            Assert.AreEqual(6, page.Items[0].PreviewVideos.Count);
            Assert.AreEqual(1356287315L, page.Items[0].PreviewVideos[0].Aid);
            Assert.AreEqual("19:49", page.Items[0].PreviewVideos[0].Duration);
            Assert.AreEqual((long?)310, page.Items[0].PreviewVideos[0].DanmakuCount);
        }

        [TestMethod]
        public void CapturedSeries_FinalMixedListPageDoesNotLoseSeries()
        {
            var page = UserSpaceCollectionParser.ParseCollections(Data("UserSpaceSeries"), 4, 2);
            Assert.AreEqual(1, page.Items.Count);
            Assert.AreEqual(UserSpaceCollectionKind.Series, page.Items[0].Kind);
            Assert.AreEqual(2005814L, page.Items[0].Id);
            Assert.IsFalse(page.HasMore);
            Assert.AreEqual("系列·中国疆域新版", page.Items[0].Heading);
            Assert.IsTrue(page.Items[0].HasPreviewVideos);
            Assert.AreEqual(762670999L, page.Items[0].PreviewVideos[0].Aid);
        }

        [DataTestMethod]
        [DataRow("UserSpaceSeasonVideos")]
        [DataRow("UserSpaceSeriesVideos")]
        public void CapturedArchives_BothPaginationShapesReturnPlayableVideos(string fixture)
        {
            var page = UserSpaceCollectionParser.ParseVideos(Data(fixture), 1, 2);
            Assert.AreEqual(2, page.Items.Count);
            Assert.IsTrue(page.HasMore);
            Assert.IsTrue(page.Items[0].Aid > 0);
            StringAssert.StartsWith(page.Items[0].Cover, "https://");
            Assert.IsFalse(string.IsNullOrEmpty(page.Items[0].Duration));
            Assert.AreEqual(fixture == "UserSpaceSeasonVideos" ? (long?)100676 : null, page.Items[0].DanmakuCount);
            Assert.AreEqual(fixture == "UserSpaceSeasonVideos" ? "100676" : "--", page.Items[0].DanmakuText);
            Assert.AreEqual(fixture == "UserSpaceSeasonVideos" ? (long?)1629596712 : 1580902333, page.Items[0].PublishedAt);
        }

        [TestMethod]
        public void SeriesWithoutCover_UsesPreviewAndPreservesSeparateIds()
        {
            var data = JObject.Parse(@"{'items_lists':{'page':{'total':2},
                'seasons_list':[{'meta':{'season_id':1,'title':'合集'}}],
                'series_list':[{'meta':{'series_id':1,'name':'系列'},'archives':[{'pic':'//i0.hdslb.com/cover.jpg'}]}]}}");
            var page = UserSpaceCollectionParser.ParseCollections(data, 1, 20);
            Assert.AreEqual(2, page.Items.Count);
            Assert.AreNotEqual(page.Items[0].Kind, page.Items[1].Kind);
            Assert.AreEqual("系列", page.Items[1].Title);
            Assert.AreEqual("https://i0.hdslb.com/cover.jpg", page.Items[1].Cover);
            Assert.IsFalse(page.HasMore);
        }

        [TestMethod]
        public void EmptyPageStopsPaginationAndInvalidAidIsNotNavigable()
        {
            var empty = UserSpaceCollectionParser.ParseVideos(JObject.Parse(@"{'page':{'total':99},'archives':[]}"), 1, 30);
            Assert.IsFalse(empty.HasMore);
            var page = UserSpaceCollectionParser.ParseVideos(JObject.Parse(@"{'page':{'total':3},'archives':[
                {'aid':0},{'aid':117404038335004,'duration':4355},{'aid':3,'duration':-1}]}"), 1, 30);
            Assert.AreEqual(2, page.Items.Count);
            Assert.AreEqual(117404038335004L, page.Items[0].Aid);
            Assert.AreEqual("1:12:35", page.Items[0].Duration);
            Assert.AreEqual("00:00", page.Items[1].Duration);
            Assert.IsFalse(page.Items[1].HasPublicationTime);
        }

        [TestMethod]
        public void MissingDataIsAnErrorAndCanBeRetried()
        {
            Assert.ThrowsException<FormatException>(() => UserSpaceCollectionParser.ParseCollections(new JObject(), 1, 20));
            Assert.ThrowsException<FormatException>(() => UserSpaceCollectionParser.ParseVideos(new JObject(), 1, 30));
        }

        [TestMethod]
        public void CollectionPreviews_KeepGroupsWithNoArchivesAndLimitPlayablePreviews()
        {
            var data = JObject.Parse(@"{'items_lists':{'page':{'total':2},'seasons_list':[
                {'meta':{'season_id':1,'title':'空合集','total':10}},
                {'meta':{'season_id':2,'title':'长合集','total':30},'archives':[
                    {'aid':0},{'aid':1},{'aid':2},{'aid':3},{'aid':4},{'aid':5},{'aid':6},{'aid':7}]}]}}");
            var page = UserSpaceCollectionParser.ParseCollections(data, 1, 20);
            Assert.AreEqual(2, page.Items.Count);
            Assert.IsFalse(page.Items[0].HasPreviewVideos);
            Assert.AreEqual(10, page.Items[0].Count);
            Assert.AreEqual(6, page.Items[1].PreviewVideos.Count);
            Assert.AreEqual(1L, page.Items[1].PreviewVideos[0].Aid);
            Assert.AreEqual(6L, page.Items[1].PreviewVideos[5].Aid);
            Assert.AreEqual(30, page.Items[1].Count);
        }
    }
}
