// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class SignatureWarningDisplayTests
    {
        private static readonly int[] DirectionalMarks = { 0x061C, 0x200E, 0x200F };

        [DataTestMethod]
        [DataRow("Example\r\nPublisher: Microsoft", "Example\\r\\nPublisher: Microsoft")]
        [DataRow("file\u202Etxt.exe", "file\\u202Etxt.exe")]
        [DataRow("--name\tvalue\u2028Run anyway\u2029", "--name\\tvalue\\u2028Run anyway\\u2029")]
        [DataRow("before\u2066inside\u2069after", "before\\u2066inside\\u2069after")]
        [DataRow("value\u0085another line", "value\\u0085another line")]
        public void DisplayEscapesMisleadingCharactersWithoutChangingRawValues(string raw, string expected)
        {
            var request = new SignatureWarningRequest
            {
                AppName = raw,
                Path = raw,
                Arguments = raw,
                Status = raw,
            };

            Assert.AreEqual(expected, request.DisplayAppName);
            Assert.AreEqual(expected, request.DisplayPath);
            Assert.AreEqual(expected, request.DisplayArguments);
            Assert.AreEqual(expected, request.DisplayStatus);
            Assert.IsTrue(request.HasEscapedDisplayValues);
            Assert.AreEqual(raw, request.AppName);
            Assert.AreEqual(raw, request.Path);
            Assert.AreEqual(raw, request.Arguments);
            Assert.AreEqual(raw, request.Status);
            StringAssert.Contains(request.DetailsText, raw);
        }

        [TestMethod]
        public void AllControlAndBidirectionalFormattingCharactersAreVisible()
        {
            var characters = Enumerable.Range(0, 32)
                .Concat(Enumerable.Range(0x7F, 33))
                .Concat(DirectionalMarks)
                .Concat(Enumerable.Range(0x2028, 7))
                .Concat(Enumerable.Range(0x2066, 10));
            foreach (var codePoint in characters)
            {
                var character = (char)codePoint;
                var expected = character switch
                {
                    '\r' => "\\r",
                    '\n' => "\\n",
                    '\t' => "\\t",
                    _ => "\\u" + codePoint.ToString("X4", CultureInfo.InvariantCulture),
                };
                var request = new SignatureWarningRequest { Path = "before" + character + "after" };

                Assert.AreEqual("before" + expected + "after", request.DisplayPath, $"U+{codePoint:X4}");
                Assert.IsTrue(request.HasEscapedDisplayValues);
            }
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(@"C:\Apps with spaces\tool.exe")]
        [DataRow(@"--name ""value"" --literal=\n --literal=\u202E")]
        [DataRow("\u05D0\u05D1\u05D2 \u0627\u0644\u0639\u0631\u0628\u064A\u0629 \u4E2D\u6587")]
        [DataRow("Emoji \U0001F469\u200D\U0001F4BB e\u0301")]
        public void OrdinaryUnicodeAndLiteralEscapeTextRemainReadable(string raw)
        {
            var request = new SignatureWarningRequest
            {
                AppName = raw,
                Path = raw,
                Arguments = raw,
                Status = raw,
            };

            Assert.AreEqual(raw, request.DisplayAppName);
            Assert.AreEqual(raw, request.DisplayPath);
            Assert.AreEqual(raw, request.DisplayArguments);
            Assert.AreEqual(raw, request.DisplayStatus);
            Assert.IsFalse(request.HasEscapedDisplayValues);
        }

        [TestMethod]
        public void CopyDetailsRetainsTheOriginalControlCharacters()
        {
            using var culture = new CultureScope("en-US");
            var request = new SignatureWarningRequest
            {
                AppName = "Example\napp",
                Path = "C:\\Apps\\file\u202Etxt.exe",
                Arguments = "--name\tvalue",
                Reason = "unsigned",
                Status = "0x800B0100",
            };

            Assert.AreEqual(
                string.Join(
                    Environment.NewLine,
                    request.AppLabel,
                    "Example\napp",
                    string.Empty,
                    request.PathLabel,
                    "C:\\Apps\\file\u202Etxt.exe",
                    string.Empty,
                    request.ReasonLabel,
                    request.ReasonText,
                    string.Empty,
                    request.ArgumentsLabel,
                    "--name\tvalue",
                    string.Empty,
                    request.StatusLabel,
                    "0x800B0100"),
                request.DetailsText);
        }
    }
}
