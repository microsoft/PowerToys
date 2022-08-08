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
        [DataRow("https://google.com", true, "https://google.com/", true)]
        [DataRow("ftps://google.com", true, "ftps://google.com/", false)]
        [DataRow("localhost", true, "https://localhost/", true)]
        [DataRow("http://localhost", true, "http://localhost/", true)]
        [DataRow("127.0.0.1", true, "https://127.0.0.1/", true)]
        [DataRow("127.0.0.1:80", true, "http://127.0.0.1/", true)]
        [DataRow("127.0.0.1:443", true, "https://127.0.0.1/", true)]
        [DataRow("http://127.0.0.1", true, "http://127.0.0.1/", true)]
        [DataRow("http://127.0.0.1/test", true, "http://127.0.0.1/test", true)]
        [DataRow("http://127.0.0.1:123/test", true, "http://127.0.0.1:123/test", true)]
        [DataRow("http://127.0.0.1/126", true, "http://127.0.0.1/126", true)]
        [DataRow("http://127.0.0.1:80", true, "http://127.0.0.1/", true)]
        [DataRow("127", false, null, false)]
        [DataRow("", false, null, false)]
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
        [DataRow("[2001:0DB8::1]:80", true, "http://[2001:db8::1]/", true)]
        [DataRow("http://[2001:0DB8::1]:80", true, "http://[2001:db8::1]/", true)]
        [DataRow("[2001:0DB8::1]:443", true, "https://[2001:db8::1]/", true)]
        [DataRow("http://[2001:0DB8::1]:443", true, "http://[2001:db8::1]:443/", true)]
        [DataRow("https://[2001:0DB8::1]:80", true, "https://[2001:db8::1]:80/", true)]
        [DataRow("mailto:example@mail.com", true, "mailto:example@mail.com", false)]
        [DataRow("ftp://example.com", true, "ftp://example.com/", false)]
        [DataRow("ftp://example.com/test", true, "ftp://example.com/test", false)]
        [DataRow("ftp://example.com:123/test", true, "ftp://example.com:123/test", false)]
        [DataRow("ftp://example.com/126", true, "ftp://example.com/126", false)]
        [DataRow("http://test.test.test.test:952", true, "http://test.test.test.test:952/", true)]
        [DataRow("https://test.test.test.test:952", true, "https://test.test.test.test:952/", true)]

        // Case where `domain:port`, as specified in issue #14260
        // Assumption: Only domain with dot is accepted
        [DataRow("example.com:80", true, "http://example.com/", true)]
        [DataRow("example.com:80/test", true, "http://example.com/test", true)]
        [DataRow("example.com:80/126", true, "http://example.com/126", true)]
        [DataRow("example.com:443", true, "https://example.com/", true)]
        [DataRow("example.com:443/test", true, "https://example.com/test", true)]
        [DataRow("example.com:443/126", true, "https://example.com/126", true)]
        [DataRow("google.com:91", true, "https://google.com:91/", true)]
        [DataRow("google.com:91/test", true, "https://google.com:91/test", true)]
        [DataRow("google.com:91/126", true, "https://google.com:91/126", true)]
        [DataRow("test.test.test.test:952", true, "https://test.test.test.test:952/", true)]
        [DataRow("test.test.test.test:952/test", true, "https://test.test.test.test:952/test", true)]
        [DataRow("test.test.test.test:952/126", true, "https://test.test.test.test:952/126", true)]

        // All following cases should be parsed as application URI
        [DataRow("tel:411", true, "tel:411", false)]
        [DataRow("tel:411/test", true, "tel:411/test", false)]
        [DataRow("tel:411/126", true, "tel:411/126", false)]
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
