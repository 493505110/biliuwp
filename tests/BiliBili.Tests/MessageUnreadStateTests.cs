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

        [TestMethod]
        public void HasUnread_仅有Feed未读且没有私信会话时也返回True()
        {
            MessageUnreadState.Reset();
            MessageUnreadState.Feed.recv_reply = 1;

            bool hasUnread = MessageUnreadState.HasUnread(Array.Empty<MessageChatModel>());

            Assert.IsTrue(hasUnread);
        }
    }
}
