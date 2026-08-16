using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class RecommendCoverDisplayTests
    {
        [TestMethod]
        public void RecommendPage_CoverUsesUniformAndLegacyImageRequest()
        {
            var pagePath = FindRepositoryFile();
            var coverLine = File.ReadAllLines(pagePath)
                .Single(line => line.Contains("ImageEx") && line.Contains("Path=cover"));

            StringAssert.Contains(coverLine, "Stretch=\"Uniform\"");
            StringAssert.Contains(coverLine, "ConverterParameter='320w_200h_1e_1c'");
            Assert.IsFalse(coverLine.Contains("UniformToFill"));
        }

        [TestMethod]
        public void RecommendPage_CoverCardUsesContentDrivenHeight()
        {
            var pagePath = FindRepositoryFile();
            var lines = File.ReadAllLines(pagePath);
            var gridLine = lines.Single(line => line.Contains("AdaptiveGridView x:Name=\"ls_feed\""));
            var rowDefinitionsIndex = Array.FindIndex(lines, line => line.Contains("<Grid.RowDefinitions>"));

            Assert.IsFalse(gridLine.Contains("ItemHeight=\"220\""));
            Assert.AreEqual("<RowDefinition Height=\"Auto\"/>", lines[rowDefinitionsIndex + 1].Trim());
        }

        private static string FindRepositoryFile()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "BiliBili.UWP",
                    "Pages",
                    "Home",
                    "RecommendPage.xaml");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            Assert.Fail("Unable to locate RecommendPage.xaml from the test output directory.");
            return null;
        }
    }
}
