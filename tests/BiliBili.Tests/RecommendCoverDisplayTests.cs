using System;
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
            var coverLine = TestRepository.ReadLines("BiliBili.UWP/Pages/Home/RecommendPage.xaml")
                .Single(line => line.Contains("ImageEx") && line.Contains("Path=cover"));

            StringAssert.Contains(coverLine, "Stretch=\"Uniform\"");
            StringAssert.Contains(coverLine, "ConverterParameter='320w_200h_1e_1c'");
            Assert.IsFalse(coverLine.Contains("UniformToFill"));
        }

        [TestMethod]
        public void RecommendPage_CoverCardUsesContentDrivenHeight()
        {
            var lines = TestRepository.ReadLines("BiliBili.UWP/Pages/Home/RecommendPage.xaml");
            var gridLine = lines.Single(line => line.Contains("AdaptiveGridView x:Name=\"ls_feed\""));
            var rowDefinitionsIndex = Array.FindIndex(lines, line => line.Contains("<Grid.RowDefinitions>"));

            Assert.IsFalse(gridLine.Contains("ItemHeight=\"220\""));
            Assert.AreEqual("<RowDefinition Height=\"Auto\"/>", lines[rowDefinitionsIndex + 1].Trim());
        }

    }
}
