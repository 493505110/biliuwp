using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    internal static class TestRepository
    {
        public static string Root
        {
            get
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BiliBili.sln")))
                {
                    directory = directory.Parent;
                }

                Assert.IsNotNull(directory, "Unable to locate the repository root.");
                return directory.FullName;
            }
        }

        public static string GetPath(string relativePath)
        {
            return Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string ReadFile(string relativePath)
        {
            return File.ReadAllText(GetPath(relativePath));
        }

        public static string[] ReadLines(string relativePath)
        {
            return File.ReadAllLines(GetPath(relativePath));
        }

        public static string ReadFixture(string fileName)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
        }

        public static string ReadMethod(string relativePath, string startMarker, string endMarker)
        {
            var source = ReadFile(relativePath);
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);

            Assert.IsTrue(start >= 0, $"Unable to find method start marker: {startMarker}");
            Assert.IsTrue(end > start, $"Unable to find method end marker: {endMarker}");
            return source.Substring(start, end - start);
        }

        public static string MethodBody(string source, string methodSignature)
        {
            var methodStart = source.IndexOf(methodSignature, StringComparison.Ordinal);
            Assert.IsTrue(methodStart >= 0, $"Unable to find method signature: {methodSignature}");

            var openingBrace = source.IndexOf('{', methodStart + methodSignature.Length);
            Assert.IsTrue(openingBrace >= 0, $"Unable to find method body: {methodSignature}");

            var braceDepth = 0;
            for (var index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    braceDepth++;
                }
                else if (source[index] == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0)
                    {
                        return source.Substring(openingBrace, index - openingBrace + 1);
                    }
                }
            }

            Assert.Fail($"Unable to find matching closing brace: {methodSignature}");
            return null;
        }

        public static string Between(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, $"Unable to find start marker: {startMarker}");

            start += startMarker.Length;
            var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.IsTrue(end >= 0, $"Unable to find end marker: {endMarker}");
            return source.Substring(start, end - start);
        }

        public static string ElementContaining(string source, string elementName, string requiredMarker)
        {
            var marker = source.IndexOf(requiredMarker, StringComparison.Ordinal);
            Assert.IsTrue(marker >= 0, $"Unable to find element marker: {requiredMarker}");

            var start = source.LastIndexOf("<" + elementName, marker, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, $"Unable to find element start: {elementName}");

            var closingMarker = "</" + elementName + ">";
            var closing = source.IndexOf(closingMarker, marker, StringComparison.Ordinal);
            if (closing >= 0)
            {
                return source.Substring(start, closing + closingMarker.Length - start);
            }

            var selfClosing = source.IndexOf("/>", marker, StringComparison.Ordinal);
            Assert.IsTrue(selfClosing >= 0, $"Unable to find element end: {elementName}");
            return source.Substring(start, selfClosing + 2 - start);
        }
    }
}
