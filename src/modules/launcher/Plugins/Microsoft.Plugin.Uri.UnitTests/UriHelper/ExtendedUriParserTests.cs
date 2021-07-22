// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Uri.UriHelper;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NUnit.Framework;

namespace Microsoft.Plugin.Uri.UnitTests.UriHelper
{
    [TestFixture]
    public class ExtendedUriParserTests
    {
        [TestCase("google.com", true, "https://google.com/")]
        [TestCase("http://google.com", true, "http://google.com/")]
        [TestCase("localhost", true, "https://localhost/")]
        [TestCase("http://localhost", true, "http://localhost/")]
        [TestCase("127.0.0.1", true, "https://127.0.0.1/")]
        [TestCase("http://127.0.0.1", true, "http://127.0.0.1/")]
        [TestCase("http://127.0.0.1:80", true, "http://127.0.0.1/")]
        [TestCase("127", false, null)]
        [TestCase("", false, null)]
        [TestCase("https://google.com", true, "https://google.com/")]
        [TestCase("ftps://google.com", true, "ftps://google.com/")]
        [TestCase(null, false, null)]
        [TestCase("bing.com/search?q=gmx", true, "https://bing.com/search?q=gmx")]
        [TestCase("http://bing.com/search?q=gmx", true, "http://bing.com/search?q=gmx")]
        [TestCase("h", true, "https://h/")]
        [TestCase("http://h", true, "http://h/")]
        [TestCase("ht", true, "https://ht/")]
        [TestCase("http://ht", true, "http://ht/")]
        [TestCase("htt", true, "https://htt/")]
        [TestCase("http://htt", true, "http://htt/")]
        [TestCase("http", true, "https://http/")]
        [TestCase("http://http", true, "http://http/")]
        [TestCase("http:", false, null)]
        [TestCase("http:/", false, null)]
        [TestCase("http://", false, null)]
        [TestCase("http://t", true, "http://t/")]
        [TestCase("http://te", true, "http://te/")]
        [TestCase("http://tes", true, "http://tes/")]
        [TestCase("http://test", true, "http://test/")]
        [TestCase("http://test.", false, null)]
        [TestCase("http://test.c", true, "http://test.c/")]
        [TestCase("http://test.co", true, "http://test.co/")]
        [TestCase("http://test.com", true, "http://test.com/")]
        [TestCase("http:3", true, "https://http:3/")]
        [TestCase("http://http:3", true, "http://http:3/")]
        [TestCase("[::]", true, "https://[::]/")]
        [TestCase("http://[::]", true, "http://[::]/")]
        [TestCase("[2001:0DB8::1]", true, "https://[2001:db8::1]/")]
        [TestCase("http://[2001:0DB8::1]", true, "http://[2001:db8::1]/")]
        [TestCase("[2001:0DB8::1]:80", true, "https://[2001:db8::1]/")]
        [TestCase("http://[2001:0DB8::1]:80", true, "http://[2001:db8::1]/")]
        public void TryParseCanParseHostName(string query, bool expectedSuccess, string expectedResult)
        {
            // Arrange
            var parser = new ExtendedUriParser();

            // Act
            var success = parser.TryParse(query, out var result);

            // Assert
            Assert.AreEqual(expectedResult, result?.ToString());
            Assert.AreEqual(expectedSuccess, success);
        }
    }
}
