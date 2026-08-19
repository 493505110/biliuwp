using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BiliBili.UWP.Models;

namespace BiliBili.Tests
{
    [TestClass]
    public class MessageUnreadStateTests
    {
        [TestCleanup]
        public void ResetState()
        {
            MessageUnreadState.Reset();
        }

        [DataTestMethod]
        [DataRow(1, 0, 0, 0, true)]
        [DataRow(0, 1, 0, 0, true)]
        [DataRow(0, 0, 1, 0, true)]
        [DataRow(0, 0, 0, 1, true)]
        [DataRow(0, 0, 0, 0, false)]
        public void HasUnread_ReportsEachFeedCategory(
            int reply,
            int at,
            int like,
            int systemMessage,
            bool expected)
        {
            MessageUnreadState.Reset();
            MessageUnreadState.Feed.recv_reply = reply;
            MessageUnreadState.Feed.at = at;
            MessageUnreadState.Feed.recv_like = like;
            MessageUnreadState.Feed.sys_msg = systemMessage;

            bool hasUnread = MessageUnreadState.HasUnread();

            Assert.AreEqual(expected, hasUnread);
        }

        [TestMethod]
        public void HasUnread_ReportsPrivateGroupAndSessionUnread()
        {
            MessageUnreadState.Reset();
            MessageUnreadState.Private.custom_unread = 1;
            Assert.IsTrue(MessageUnreadState.HasUnread());

            MessageUnreadState.Reset();
            MessageUnreadState.Group.unread_count = 1;
            Assert.IsTrue(MessageUnreadState.HasUnread());

            MessageUnreadState.Reset();
            Assert.IsTrue(MessageUnreadState.HasUnread(new[]
            {
                new MessageChatModel { msg_count = 1 }
            }));
        }

        [TestMethod]
        public void HasUnread_IgnoresNullAndEmptySessions()
        {
            MessageUnreadState.Reset();

            Assert.IsFalse(MessageUnreadState.HasUnread(null));
            Assert.IsFalse(MessageUnreadState.HasUnread(new MessageChatModel[]
            {
                null,
                new MessageChatModel { msg_count = 0 }
            }));
        }
    }
}
