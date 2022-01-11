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
        [DataRow("google.com", true, "https://google.com/", true)]
        [DataRow("http://google.com", true, "http://google.com/", true)]
        [DataRow("localhost", true, "https://localhost/", true)]
        [DataRow("http://localhost", true, "http://localhost/", true)]
        [DataRow("127.0.0.1", true, "https://127.0.0.1/", true)]
        [DataRow("http://127.0.0.1", true, "http://127.0.0.1/", true)]
        [DataRow("http://127.0.0.1:80", true, "http://127.0.0.1/", true)]
        [DataRow("127", false, null, false)]
        [DataRow("", false, null, false)]
        [DataRow("https://google.com", true, "https://google.com/", true)]
        [DataRow("ftps://google.com", true, "ftps://google.com/", false)]
        [DataRow(null, false, null, false)]
        [DataRow("bing.com/search?q=gmx", true, "https://bing.com/search?q=gmx", true)]
        [DataRow("http://bing.com/search?q=gmx", true, "http://bing.com/search?q=gmx", true)]
        [DataRow("h", true, "https://h/", true)]
        [DataRow("http://h", true, "http://h/", true)]
        [DataRow("ht", true, "https://ht/", true)]
        [DataRow("http://ht", true, "http://ht/", true)]
        [DataRow("htt", true, "https://htt/", true)]
        [DataRow("http://htt", true, "http://htt/", true)]
        [DataRow("http", true, "https://http/", true)]
        [DataRow("http://http", true, "http://http/", true)]
        [DataRow("http:", false, null, false)]
        [DataRow("http:/", false, null, false)]
        [DataRow("http://", false, null, false)]
        [DataRow("http://t", true, "http://t/", true)]
        [DataRow("http://te", true, "http://te/", true)]
        [DataRow("http://tes", true, "http://tes/", true)]
        [DataRow("http://test", true, "http://test/", true)]
        [DataRow("http://test.", false, null, false)]
        [DataRow("http://test.c", true, "http://test.c/", true)]
        [DataRow("http://test.co", true, "http://test.co/", true)]
        [DataRow("http://test.com", true, "http://test.com/", true)]
        [DataRow("http:3", true, "https://http:3/", true)]
        [DataRow("http://http:3", true, "http://http:3/", true)]
        [DataRow("[::]", true, "https://[::]/", true)]
        [DataRow("http://[::]", true, "http://[::]/", true)]
        [DataRow("[2001:0DB8::1]", true, "https://[2001:db8::1]/", true)]
        [DataRow("http://[2001:0DB8::1]", true, "http://[2001:db8::1]/", true)]
        [DataRow("[2001:0DB8::1]:80", true, "https://[2001:db8::1]/", true)]
        [DataRow("http://[2001:0DB8::1]:80", true, "http://[2001:db8::1]/", true)]
        [DataRow("mailto:example@mail.com", true, "mailto:example@mail.com", false)]
        [DataRow("tel:411", true, "tel:411", false)]
        [DataRow("ftp://example.com", true, "ftp://example.com/", false)]

        // This has been parsed as an application URI. Linked issue: #14260
        [DataRow("example.com:443", true, "example.com:443", false)]
        [DataRow("mailto:", true, "mailto:", false)]
        [DataRow("mailto:/", false, null, false)]
        [DataRow("ms-settings:", true, "ms-settings:", false)]
        [DataRow("ms-settings:/", false, null, false)]
        [DataRow("ms-settings://", false, null, false)]
        [DataRow("ms-settings://privacy", true, "ms-settings://privacy/", false)]
        [DataRow("ms-settings://privacy/", true, "ms-settings://privacy/", false)]
        [DataRow("ms-settings:privacy", true, "ms-settings:privacy", false)]
        [DataRow("ms-settings:powersleep", true, "ms-settings:powersleep", false)]
        [DataRow("microsoft-edge:http://google.com", true, "microsoft-edge:http://google.com", false)]
        [DataRow("microsoft-edge:https://google.com", true, "microsoft-edge:https://google.com", false)]
        [DataRow("microsoft-edge:google.com", true, "microsoft-edge:google.com", false)]
        [DataRow("microsoft-edge:google.com/", true, "microsoft-edge:google.com/", false)]
        [DataRow("microsoft-edge:https://google.com/", true, "microsoft-edge:https://google.com/", false)]
        [DataRow("ftp://user:password@localhost:8080", true, "ftp://user:password@localhost:8080/", false)]
        [DataRow("ftp://user:password@localhost:8080/", true, "ftp://user:password@localhost:8080/", false)]
        [DataRow("ftp://user:password@google.com", true, "ftp://user:password@google.com/", false)]
        [DataRow("ftp://user:password@google.com:2121", true, "ftp://user:password@google.com:2121/", false)]
        [DataRow("ftp://user:password@1.1.1.1", true, "ftp://user:password@1.1.1.1/", false)]
        [DataRow("ftp://user:password@1.1.1.1:2121", true, "ftp://user:password@1.1.1.1:2121/", false)]
        [DataRow("^:", false, null, false)]

        public void TryParseCanParseHostName(string query, bool expectedSuccess, string expectedResult, bool expectedIsWebUri)
        {
            // Arrange
            var parser = new ExtendedUriParser();

            // Act
            var success = parser.TryParse(query, out var result, out var isWebUriResult);

            // Assert
            Assert.AreEqual(expectedResult, result?.ToString());
            Assert.AreEqual(expectedIsWebUri, isWebUriResult);
            Assert.AreEqual(expectedSuccess, success);
        }
    }
}
