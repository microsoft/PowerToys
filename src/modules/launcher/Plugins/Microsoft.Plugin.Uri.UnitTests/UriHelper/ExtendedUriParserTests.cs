// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Uri.UriHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Plugin.Uri.UnitTests.UriHelper
{
    [TestClass]
    public class ExtendedUriParserTests
    {
        [DataTestMethod]
        [DataRow("google.com", true, "https://google.com/")]
        [DataRow("http://google.com", true, "http://google.com/")]
        [DataRow("localhost", true, "https://localhost/")]
        [DataRow("http://localhost", true, "http://localhost/")]
        [DataRow("127.0.0.1", true, "https://127.0.0.1/")]
        [DataRow("http://127.0.0.1", true, "http://127.0.0.1/")]
        [DataRow("http://127.0.0.1:80", true, "http://127.0.0.1/")]
        [DataRow("127", false, null)]
        [DataRow("", false, null)]
        [DataRow("https://google.com", true, "https://google.com/")]
        [DataRow("ftps://google.com", true, "ftps://google.com/")]
        [DataRow(null, false, null)]
        [DataRow("bing.com/search?q=gmx", true, "https://bing.com/search?q=gmx")]
        [DataRow("http://bing.com/search?q=gmx", true, "http://bing.com/search?q=gmx")]
        [DataRow("h", true, "https://h/")]
        [DataRow("http://h", true, "http://h/")]
        [DataRow("ht", true, "https://ht/")]
        [DataRow("http://ht", true, "http://ht/")]
        [DataRow("htt", true, "https://htt/")]
        [DataRow("http://htt", true, "http://htt/")]
        [DataRow("http", true, "https://http/")]
        [DataRow("http://http", true, "http://http/")]
        [DataRow("http:", false, null)]
        [DataRow("http:/", false, null)]
        [DataRow("http://", false, null)]
        [DataRow("http://t", true, "http://t/")]
        [DataRow("http://te", true, "http://te/")]
        [DataRow("http://tes", true, "http://tes/")]
        [DataRow("http://test", true, "http://test/")]
        [DataRow("http://test.", false, null)]
        [DataRow("http://test.c", true, "http://test.c/")]
        [DataRow("http://test.co", true, "http://test.co/")]
        [DataRow("http://test.com", true, "http://test.com/")]
        [DataRow("http:3", true, "https://http:3/")]
        [DataRow("http://http:3", true, "http://http:3/")]
        [DataRow("[::]", true, "https://[::]/")]
        [DataRow("http://[::]", true, "http://[::]/")]
        [DataRow("[2001:0DB8::1]", true, "https://[2001:db8::1]/")]
        [DataRow("http://[2001:0DB8::1]", true, "http://[2001:db8::1]/")]
        [DataRow("[2001:0DB8::1]:80", true, "https://[2001:db8::1]/")]
        [DataRow("http://[2001:0DB8::1]:80", true, "http://[2001:db8::1]/")]
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
