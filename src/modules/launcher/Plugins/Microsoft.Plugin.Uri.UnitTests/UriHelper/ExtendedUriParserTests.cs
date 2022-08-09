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
        [DataRow("google.com", true, "https://google.com/", true, null)]
        [DataRow("http://google.com", true, "http://google.com/", true, null)]
        [DataRow("https://google.com", true, "https://google.com/", true, null)]
        [DataRow("ftps://google.com", true, "ftps://google.com/", false, null)]
        [DataRow("bing.com/search?q=gmx", true, "https://bing.com/search?q=gmx", true, null)]
        [DataRow("http://bing.com/search?q=gmx", true, "http://bing.com/search?q=gmx", true, null)]

        [DataRow("127", false, null, false, null)]
        [DataRow(null, false, null, false, null)]
        [DataRow("h", true, "https://h/", true, null)]
        [DataRow("ht", true, "https://ht/", true, null)]
        [DataRow("htt", true, "https://htt/", true, null)]
        [DataRow("http", true, "https://http/", true, null)]
        [DataRow("http:", false, null, false, null)]
        [DataRow("http:/", false, null, false, null)]
        [DataRow("http://", false, null, false, null)]
        [DataRow("http://h", true, "http://h/", true, null)]
        [DataRow("http://ht", true, "http://ht/", true, null)]
        [DataRow("http://htt", true, "http://htt/", true, null)]
        [DataRow("http://http", true, "http://http/", true, null)]
        [DataRow("http://t", true, "http://t/", true, null)]
        [DataRow("http://te", true, "http://te/", true, null)]
        [DataRow("http://tes", true, "http://tes/", true, null)]
        [DataRow("http://test", true, "http://test/", true, null)]
        [DataRow("http://test.", false, null, false, null)]
        [DataRow("http://test.c", true, "http://test.c/", true, null)]
        [DataRow("http://test.co", true, "http://test.co/", true, null)]
        [DataRow("http://test.com", true, "http://test.com/", true, null)]
        [DataRow("http:3", true, "https://http:3/", true, null)]
        [DataRow("http://http:3", true, "http://http:3/", true, null)]
        [DataRow("[2001:0DB8::1]", true, "https://[2001:db8::1]/", true, null)]
        [DataRow("[2001:0DB8::1]:80", true, "http://[2001:db8::1]/", true, null)]
        [DataRow("[2001:0DB8::1]:443", true, "https://[2001:db8::1]/", true, null)]
        [DataRow("http://[2001:0DB8::1]", true, "http://[2001:db8::1]/", true, null)]
        [DataRow("http://[2001:0DB8::1]:80", true, "http://[2001:db8::1]/", true, null)]
        [DataRow("http://[2001:0DB8::1]:443", true, "http://[2001:db8::1]:443/", true, null)]
        [DataRow("https://[2001:0DB8::1]:80", true, "https://[2001:db8::1]:80/", true, null)]
        [DataRow("mailto:example@mail.com", true, "mailto:example@mail.com", false, null)]
        [DataRow("ftp://example.com", true, "ftp://example.com/", false, null)]
        [DataRow("ftp://example.com/test", true, "ftp://example.com/test", false, null)]
        [DataRow("ftp://example.com:123/test", true, "ftp://example.com:123/test", false, null)]
        [DataRow("ftp://example.com/126", true, "ftp://example.com/126", false, null)]
        [DataRow("http://test.test.test.test:952", true, "http://test.test.test.test:952/", true, null)]
        [DataRow("https://test.test.test.test:952", true, "https://test.test.test.test:952/", true, null)]

        // ToDo: Block [::] address results in parser. This Address is unspecified per RFC 4291 and the results make no sense.
        [DataRow("[::]", true, "https://[::]/", true, null)]
        [DataRow("http://[::]", true, "http://[::]/", true, null)]

        // localhost, 127.0.0.1, ::1 tests
        [DataRow("localhost", true, "https://localhost/", true, null)]
        [DataRow("localhost:80", true, "http://localhost/", true, null)]
        [DataRow("localhost:443", true, "https://localhost/", true, null)]
        [DataRow("localhost:1234", true, "https://localhost:1234/", true, null)]
        [DataRow("localhost/test", true, "https://localhost/test", true, null)]
        [DataRow("localhost:80/test", true, "http://localhost/test", true, null)]
        [DataRow("localhost:443/test", true, "https://localhost/test", true, null)]
        [DataRow("localhost:1234/test", true, "https://localhost:1234/test", true, null)]
        [DataRow("http://localhost", true, "http://localhost/", true, null)]
        [DataRow("http://localhost:1234", true, "http://localhost:1234/", true, null)]
        [DataRow("https://localhost", true, "https://localhost/", true, null)]
        [DataRow("https://localhost:1234", true, "https://localhost:1234/", true, null)]
        [DataRow("http://localhost/test", true, "http://localhost/test", true, null)]
        [DataRow("http://localhost:1234/test", true, "http://localhost:1234/test", true, null)]
        [DataRow("https://localhost/test", true, "https://localhost/test", true, null)]
        [DataRow("https://localhost:1234/test", true, "https://localhost:1234/test", true, null)]
        [DataRow("127.0.0.1", true, "https://127.0.0.1/", true, null)]
        [DataRow("127.0.0.1:80", true, "http://127.0.0.1/", true, null)]
        [DataRow("127.0.0.1:443", true, "https://127.0.0.1/", true, null)]
        [DataRow("127.0.0.1:1234", true, "https://127.0.0.1:1234/", true, null)]
        [DataRow("127.0.0.1/test", true, "https://127.0.0.1/test", true, null)]
        [DataRow("127.0.0.1:80/test", true, "http://127.0.0.1/test", true, null)]
        [DataRow("127.0.0.1:443/test", true, "https://127.0.0.1/test", true, null)]
        [DataRow("127.0.0.1:1234/test", true, "https://127.0.0.1:1234/test", true, null)]
        [DataRow("http://127.0.0.1", true, "http://127.0.0.1/", true, null)]
        [DataRow("http://127.0.0.1:1234", true, "http://127.0.0.1:1234/", true, null)]
        [DataRow("https://127.0.0.1", true, "https://127.0.0.1/", true, null)]
        [DataRow("https://127.0.0.1:1234", true, "https://127.0.0.1:1234/", true, null)]
        [DataRow("http://127.0.0.1/test", true, "http://127.0.0.1/test", true, null)]
        [DataRow("http://127.0.0.1:1234/test", true, "http://127.0.0.1:1234/test", true, null)]
        [DataRow("https://127.0.0.1/test", true, "https://127.0.0.1/test", true, null)]
        [DataRow("https://127.0.0.1:1234/test", true, "https://127.0.0.1:1234/test", true, null)]
        [DataRow("[::1]", true, "https://[::1]/", true, null)]
        [DataRow("[::1]:80", true, "http://[::1]/", true, null)]
        [DataRow("[::1]:443", true, "https://[::1]/", true, null)]
        [DataRow("[::1]:1234", true, "https://[::1]:1234/", true, null)]
        [DataRow("[::1]/test", true, "https://[::1]/test", true, null)]
        [DataRow("[::1]:80/test", true, "http://[::1]/test", true, null)]
        [DataRow("[::1]:443/test", true, "https://[::1]/test", true, null)]
        [DataRow("[::1]:1234/test", true, "https://[::1]:1234/test", true, null)]
        [DataRow("http://[::1]", true, "http://[::1]/", true, null)]
        [DataRow("http://[::1]:1234", true, "http://[::1]:1234/", true, null)]
        [DataRow("https://[::1]", true, "https://[::1]/", true, null)]
        [DataRow("https://[::1]:1234", true, "https://[::1]:1234/", true, null)]
        [DataRow("http://[::1]/test", true, "http://[::1]/test", true, null)]
        [DataRow("http://[::1]:1234/test", true, "http://[::1]:1234/test", true, null)]
        [DataRow("https://[::1]/test", true, "https://[::1]/test", true, null)]
        [DataRow("https://[::1]:1234/test", true, "https://[::1]:1234/test", true, null)]

        // Case where `domain:port`, as specified in issue #14260
        // Assumption: Only domain with dot is accepted
        [DataRow("example.com:80", true, "http://example.com/", true, null)]
        [DataRow("example.com:80/test", true, "http://example.com/test", true, null)]
        [DataRow("example.com:80/126", true, "http://example.com/126", true, null)]
        [DataRow("example.com:443", true, "https://example.com/", true, null)]
        [DataRow("example.com:443/test", true, "https://example.com/test", true, null)]
        [DataRow("example.com:443/126", true, "https://example.com/126", true, null)]
        [DataRow("google.com:91", true, "https://google.com:91/", true, null)]
        [DataRow("google.com:91/test", true, "https://google.com:91/test", true, null)]
        [DataRow("google.com:91/126", true, "https://google.com:91/126", true, null)]
        [DataRow("test.test.test.test:952", true, "https://test.test.test.test:952/", true, null)]
        [DataRow("test.test.test.test:952/test", true, "https://test.test.test.test:952/test", true, null)]
        [DataRow("test.test.test.test:952/126", true, "https://test.test.test.test:952/126", true, null)]

        // Following cases can be both interpreted as schema:path and domain:port
        [DataRow("tel:411", true, "https://tel:411/", true, "tel:411")]
        [DataRow("tel:70421567", true, "tel:70421567", false, null)]
        [DataRow("tel:863-1234", true, "tel:863-1234", false, null)]
        [DataRow("tel:tel:+1-201-555-0123", true, "tel:tel:+1-201-555-0123", false, null)]

        // All following cases should be parsed as application URI
        [DataRow("mailto:", true, "mailto:", false, null)]
        [DataRow("mailto:/", false, null, false, null)]
        [DataRow("ms-settings:", true, "ms-settings:", false, null)]
        [DataRow("ms-settings:/", false, null, false, null)]
        [DataRow("ms-settings://", false, null, false, null)]
        [DataRow("ms-settings://privacy", true, "ms-settings://privacy/", false, null)]
        [DataRow("ms-settings://privacy/", true, "ms-settings://privacy/", false, null)]
        [DataRow("ms-settings:privacy", true, "ms-settings:privacy", false, null)]
        [DataRow("ms-settings:powersleep", true, "ms-settings:powersleep", false, null)]
        [DataRow("microsoft-edge:http://google.com", true, "microsoft-edge:http://google.com", false, null)]
        [DataRow("microsoft-edge:https://google.com", true, "microsoft-edge:https://google.com", false, null)]
        [DataRow("microsoft-edge:google.com", true, "microsoft-edge:google.com", false, null)]
        [DataRow("microsoft-edge:google.com/", true, "microsoft-edge:google.com/", false, null)]
        [DataRow("microsoft-edge:https://google.com/", true, "microsoft-edge:https://google.com/", false, null)]
        [DataRow("ftp://user:password@localhost:8080", true, "ftp://user:password@localhost:8080/", false, null)]
        [DataRow("ftp://user:password@localhost:8080/", true, "ftp://user:password@localhost:8080/", false, null)]
        [DataRow("ftp://user:password@google.com", true, "ftp://user:password@google.com/", false, null)]
        [DataRow("ftp://user:password@google.com:2121", true, "ftp://user:password@google.com:2121/", false, null)]
        [DataRow("ftp://user:password@1.1.1.1", true, "ftp://user:password@1.1.1.1/", false, null)]
        [DataRow("ftp://user:password@1.1.1.1:2121", true, "ftp://user:password@1.1.1.1:2121/", false, null)]
        [DataRow("^:", false, null, false, null)]

        public void TryParseCanParseHostName(string query, bool expectedSuccess, string expectedResult, bool expectedIsWebUri, string expectedSecondResult)
        {
            // Arrange
            var parser = new ExtendedUriParser();

            // Act
            var success = parser.TryParse(query, out var result, out var isWebUriResult, out var secondResult);

            // Assert
            Assert.AreEqual(expectedResult, result?.ToString());
            Assert.AreEqual(expectedIsWebUri, isWebUriResult);
            Assert.AreEqual(expectedSuccess, success);
            Assert.AreEqual(expectedSecondResult, secondResult?.ToString());
        }
    }
}
