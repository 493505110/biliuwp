using BiliBili.UWP.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class LoginCredentialTests
    {
        [TestMethod]
        public void LegacyAccessKeyAllowsRefreshTokenMigration()
        {
            var legacy = LoginCredential.Parse("legacy-access");
            Assert.AreEqual("legacy-access", legacy.AccessToken);
            Assert.IsNull(legacy.RefreshToken);
        }

        [DataTestMethod]
        [DataRow("refresh", "refresh")]
        [DataRow(null, "")]
        public void SavedPairPreservesAccessAndDoesNotInheritAnOldRefreshToken(string refresh, string expected)
        {
            var saved = new LoginCredential("new-access", refresh).Serialize();
            var pair = LoginCredential.Parse(saved);
            Assert.AreEqual("new-access", pair.AccessToken);
            Assert.AreEqual(expected, pair.RefreshToken);
        }
    }
}
