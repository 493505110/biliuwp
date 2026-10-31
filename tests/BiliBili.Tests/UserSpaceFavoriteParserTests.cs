using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace BiliBili.Tests
{
    [TestClass]
    public class UserSpaceFavoriteParserTests
    {
        private static JObject Response(string fixture) =>
            JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture + ".json")));

        private static JObject Data(string fixture) => (JObject)Response(fixture)["data"];

        [TestMethod]
        public void CapturedFolders_UseMediaIdInsteadOfFidAndRecognizePublicFlags()
        {
            var folders = UserSpaceFavoriteParser.ParseFoldersResponse(Response("UserSpaceFavoriteFolders"));
            Assert.AreEqual(4, folders.Count);
            Assert.AreEqual(4026748432L, folders[0].Id);
            Assert.AreEqual("B站 × WAIC AI会客厅", folders[0].Title);
            Assert.AreEqual("公开 · 10 个视频", folders[0].Summary);
            Assert.AreEqual("公开 · 15 个视频", folders[3].Summary);
        }

        [TestMethod]
        public void CapturedVideos_ParseFavoriteStatisticsAndLongVideoDuration()
        {
            var page = UserSpaceFavoriteParser.ParseVideos(Data("UserSpaceFavoriteVideos"), 1, 2);
            Assert.AreEqual(2, page.Videos.Count);
            Assert.AreEqual(116980698843032L, page.Videos[0].Aid);
            Assert.AreEqual("4:37:52", page.Videos[0].Duration);
            Assert.AreEqual((long?)5770, page.Videos[0].DanmakuCount);
            Assert.AreEqual((long?)1784985578, page.Videos[0].PublishedAt);
            StringAssert.StartsWith(page.Videos[0].Cover, "https://");
            Assert.IsTrue(page.Videos[0].CanOpen);
            Assert.IsTrue(page.HasMore);
        }

        [TestMethod]
        public void EmptyFavorites_AreSuccessfulEmptyLists()
        {
            Assert.AreEqual(0, UserSpaceFavoriteParser.ParseFoldersResponse(Response("UserSpaceFavoriteEmptyFolders")).Count);
            Assert.AreEqual(0, UserSpaceFavoriteParser.ParseFoldersResponse(JObject.Parse(@"{'code':0,'data':{'count':0,'list':null}}")).Count);
            Assert.AreEqual(0, UserSpaceFavoriteParser.ParseFolders(JObject.Parse(@"{'count':0,'list':null}")).Count);
            var page = UserSpaceFavoriteParser.ParseVideos(JObject.Parse(@"{'medias':null,'has_more':false}"), 1);
            Assert.AreEqual(0, page.Videos.Count);
            Assert.IsFalse(page.HasMore);
        }

        [DataTestMethod]
        [DataRow(-101, "账号未登录")]
        [DataRow(-403, "访问权限不足")]
        public void FailedFolderResponse_WithNullData_RemainsAnError(int code, string message)
        {
            var response = new JObject { ["code"] = code, ["message"] = message, ["data"] = JValue.CreateNull() };
            var error = Assert.ThrowsException<InvalidOperationException>(() => UserSpaceFavoriteParser.ParseFoldersResponse(response));
            Assert.AreEqual(message, error.Message);
        }

        [DataTestMethod]
        [DataRow("{}")]
        [DataRow("{'data':null}")]
        [DataRow("{'code':'0','data':null}")]
        [DataRow("{'code':0}")]
        [DataRow("{'code':0,'data':[]}")]
        [DataRow("{'code':0,'data':{}}")]
        public void MalformedFolderResponse_IsNotTreatedAsEmptyFavorites(string json)
        {
            Assert.ThrowsException<FormatException>(() => UserSpaceFavoriteParser.ParseFoldersResponse(JObject.Parse(json)));
        }

        [TestMethod]
        public void NonVideoMedia_DoesNotEndPaginationBeforeServerSaysSo()
        {
            var page = UserSpaceFavoriteParser.ParseVideos(JObject.Parse(@"{'medias':[{'id':123,'type':12}],'has_more':true}"), 1);
            Assert.AreEqual(0, page.Videos.Count);
            Assert.IsTrue(page.HasMore);
        }

        [TestMethod]
        public void InvalidVideo_RemainsVisibleButCannotOpen()
        {
            var page = UserSpaceFavoriteParser.ParseVideos(JObject.Parse(@"{'medias':[
                {'id':1,'type':2,'attr':9,'title':'已失效视频'},{'id':2,'type':2,'attr':0}],'has_more':false}"), 1);
            Assert.AreEqual(2, page.Videos.Count);
            Assert.IsFalse(page.Videos[0].CanOpen);
            Assert.IsTrue(page.Videos[1].CanOpen);
            Assert.AreEqual("--", page.Videos[0].DanmakuText);
        }

        [TestMethod]
        public void CountFallbackUsesRawMediaPageAndPrivateFlagIsDistinct()
        {
            var data = JObject.Parse(@"{'info':{'media_count':21},'medias':[{'id':1,'type':12}]}");
            Assert.IsTrue(UserSpaceFavoriteParser.ParseVideos(data, 1).HasMore);
            Assert.IsFalse(UserSpaceFavoriteParser.ParseVideos(data, 2).HasMore);
            var folders = UserSpaceFavoriteParser.ParseFolders(JObject.Parse(@"{'count':1,'list':[{'id':1,'title':'私密夹','attr':0}]}"));
            Assert.AreEqual("私密 · 0 个视频", folders[0].Summary);
        }

        [TestMethod]
        public void MissingResponseData_IsNotTreatedAsEmptyFavorites()
        {
            Assert.ThrowsException<FormatException>(() => UserSpaceFavoriteParser.ParseFoldersResponse(null));
            Assert.ThrowsException<FormatException>(() => UserSpaceFavoriteParser.ParseFolders(null));
            Assert.ThrowsException<FormatException>(() => UserSpaceFavoriteParser.ParseFolders(new JObject()));
            Assert.ThrowsException<FormatException>(() => UserSpaceFavoriteParser.ParseVideos(null, 1));
            Assert.ThrowsException<FormatException>(() => UserSpaceFavoriteParser.ParseVideos(new JObject(), 1));
        }
    }
}
