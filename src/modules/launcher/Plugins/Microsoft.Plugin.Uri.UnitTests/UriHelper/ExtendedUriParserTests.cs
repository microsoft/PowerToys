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

        // Standard web uri
        [DataRow("google.com", true, "https://google.com/", null)]
        [DataRow("http://google.com", true, "http://google.com/", null)]
        [DataRow("https://google.com", true, "https://google.com/", null)]
        [DataRow("ftps://google.com", true, null, "ftps://google.com/")]
        [DataRow("bing.com/search?q=gmx", true, "https://bing.com/search?q=gmx", null)]
        [DataRow("http://bing.com/search?q=gmx", true, "http://bing.com/search?q=gmx", null)]

        // Edge cases
        [DataRow("127", false, null, null)]
        [DataRow(null, false, null, null)]
        [DataRow("h", true, "https://h/", null)]
        [DataRow("ht", true, "https://ht/", null)]
        [DataRow("htt", true, "https://htt/", null)]
        [DataRow("http", true, "https://http/", null)]
        [DataRow("http:", false, null, null)]
        [DataRow("http:/", false, null, null)]
        [DataRow("http://", false, null, null)]
        [DataRow("http://h", true, "http://h/", null)]
        [DataRow("http://ht", true, "http://ht/", null)]
        [DataRow("http://htt", true, "http://htt/", null)]
        [DataRow("http://http", true, "http://http/", null)]
        [DataRow("http://t", true, "http://t/", null)]
        [DataRow("http://te", true, "http://te/", null)]
        [DataRow("http://tes", true, "http://tes/", null)]
        [DataRow("http://test", true, "http://test/", null)]
        [DataRow("http://test.", false, null, null)]
        [DataRow("http://test.c", true, "http://test.c/", null)]
        [DataRow("http://test.co", true, "http://test.co/", null)]
        [DataRow("http://test.com", true, "http://test.com/", null)]
        [DataRow("http:3", true, "https://http:3/", null)]
        [DataRow("http://http:3", true, "http://http:3/", null)]
        [DataRow("[2001:0DB8::1]", true, "https://[2001:db8::1]/", null)]
        [DataRow("[2001:0DB8::1]:80", true, "http://[2001:db8::1]/", null)]
        [DataRow("[2001:0DB8::1]:443", true, "https://[2001:db8::1]/", null)]
        [DataRow("http://[2001:0DB8::1]", true, "http://[2001:db8::1]/", null)]
        [DataRow("http://[2001:0DB8::1]:80", true, "http://[2001:db8::1]/", null)]
        [DataRow("http://[2001:0DB8::1]:443", true, "http://[2001:db8::1]:443/", null)]
        [DataRow("https://[2001:0DB8::1]:80", true, "https://[2001:db8::1]:80/", null)]
        [DataRow("http://test.test.test.test:952", true, "http://test.test.test.test:952/", null)]
        [DataRow("https://test.test.test.test:952", true, "https://test.test.test.test:952/", null)]

        // ToDo: Block [::] address results in parser. This Address is unspecified per RFC 4291 and the results make no sense.
        [DataRow("[::]", true, "https://[::]/", null)]
        [DataRow("http://[::]", true, "http://[::]/", null)]

        // localhost, 127.0.0.1, ::1 tests
        [DataRow("localhost", true, "https://localhost/", null)]
        [DataRow("localhost:80", true, "http://localhost/", null)]
        [DataRow("localhost:443", true, "https://localhost/", null)]
        [DataRow("localhost:1234", true, "https://localhost:1234/", null)]
        [DataRow("localhost/test", true, "https://localhost/test", null)]
        [DataRow("localhost:80/test", true, "http://localhost/test", null)]
        [DataRow("localhost:443/test", true, "https://localhost/test", null)]
        [DataRow("localhost:1234/test", true, "https://localhost:1234/test", null)]
        [DataRow("http://localhost", true, "http://localhost/", null)]
        [DataRow("http://localhost:1234", true, "http://localhost:1234/", null)]
        [DataRow("https://localhost", true, "https://localhost/", null)]
        [DataRow("https://localhost:1234", true, "https://localhost:1234/", null)]
        [DataRow("http://localhost/test", true, "http://localhost/test", null)]
        [DataRow("http://localhost:1234/test", true, "http://localhost:1234/test", null)]
        [DataRow("https://localhost/test", true, "https://localhost/test", null)]
        [DataRow("https://localhost:1234/test", true, "https://localhost:1234/test", null)]
        [DataRow("127.0.0.1", true, "https://127.0.0.1/", null)]
        [DataRow("127.0.0.1:80", true, "http://127.0.0.1/", null)]
        [DataRow("127.0.0.1:443", true, "https://127.0.0.1/", null)]
        [DataRow("127.0.0.1:1234", true, "https://127.0.0.1:1234/", null)]
        [DataRow("127.0.0.1/test", true, "https://127.0.0.1/test", null)]
        [DataRow("127.0.0.1:80/test", true, "http://127.0.0.1/test", null)]
        [DataRow("127.0.0.1:443/test", true, "https://127.0.0.1/test", null)]
        [DataRow("127.0.0.1:1234/test", true, "https://127.0.0.1:1234/test", null)]
        [DataRow("http://127.0.0.1", true, "http://127.0.0.1/", null)]
        [DataRow("http://127.0.0.1:1234", true, "http://127.0.0.1:1234/", null)]
        [DataRow("https://127.0.0.1", true, "https://127.0.0.1/", null)]
        [DataRow("https://127.0.0.1:1234", true, "https://127.0.0.1:1234/", null)]
        [DataRow("http://127.0.0.1/test", true, "http://127.0.0.1/test", null)]
        [DataRow("http://127.0.0.1:1234/test", true, "http://127.0.0.1:1234/test", null)]
        [DataRow("https://127.0.0.1/test", true, "https://127.0.0.1/test", null)]
        [DataRow("https://127.0.0.1:1234/test", true, "https://127.0.0.1:1234/test", null)]
        [DataRow("[::1]", true, "https://[::1]/", null)]
        [DataRow("[::1]:80", true, "http://[::1]/", null)]
        [DataRow("[::1]:443", true, "https://[::1]/", null)]
        [DataRow("[::1]:1234", true, "https://[::1]:1234/", null)]
        [DataRow("[::1]/test", true, "https://[::1]/test", null)]
        [DataRow("[::1]:80/test", true, "http://[::1]/test", null)]
        [DataRow("[::1]:443/test", true, "https://[::1]/test", null)]
        [DataRow("[::1]:1234/test", true, "https://[::1]:1234/test", null)]
        [DataRow("http://[::1]", true, "http://[::1]/", null)]
        [DataRow("http://[::1]:1234", true, "http://[::1]:1234/", null)]
        [DataRow("https://[::1]", true, "https://[::1]/", null)]
        [DataRow("https://[::1]:1234", true, "https://[::1]:1234/", null)]
        [DataRow("http://[::1]/test", true, "http://[::1]/test", null)]
        [DataRow("http://[::1]:1234/test", true, "http://[::1]:1234/test", null)]
        [DataRow("https://[::1]/test", true, "https://[::1]/test", null)]
        [DataRow("https://[::1]:1234/test", true, "https://[::1]:1234/test", null)]

        // Case where `domain:port`, as specified in issue #14260
        // Assumption: Only domain with dot is accepted as sole webUri
        [DataRow("example.com:80", true, "http://example.com/", null)]
        [DataRow("example.com:80/test", true, "http://example.com/test", null)]
        [DataRow("example.com:80/126", true, "http://example.com/126", null)]
        [DataRow("example.com:443", true, "https://example.com/", null)]
        [DataRow("example.com:443/test", true, "https://example.com/test", null)]
        [DataRow("example.com:443/126", true, "https://example.com/126", null)]
        [DataRow("google.com:91", true, "https://google.com:91/", null)]
        [DataRow("google.com:91/test", true, "https://google.com:91/test", null)]
        [DataRow("google.com:91/126", true, "https://google.com:91/126", null)]
        [DataRow("test.test.test.test:952", true, "https://test.test.test.test:952/", null)]
        [DataRow("test.test.test.test:952/test", true, "https://test.test.test.test:952/test", null)]
        [DataRow("test.test.test.test:952/126", true, "https://test.test.test.test:952/126", null)]

        // Following cases can be both interpreted as schema:path and domain:port
        [DataRow("tel:411", true, "https://tel:411/", "tel:411")]
        [DataRow("tel:70421567", true, null, "tel:70421567")]
        [DataRow("tel:863-1234", true, null, "tel:863-1234")]
        [DataRow("tel:+1-201-555-0123", true, null, "tel:+1-201-555-0123")]

        // All following cases should be parsed as application URI
        [DataRow("mailto:", true, null, "mailto:")]
        [DataRow("mailto:/", false, null, null)]
        [DataRow("mailto:example@mail.com", true, null, "mailto:example@mail.com")]
        [DataRow("ms-settings:", true, null, "ms-settings:")]
        [DataRow("ms-settings:/", false, null, null)]
        [DataRow("ms-settings://", false, null, null)]
        [DataRow("ms-settings://privacy", true, null, "ms-settings://privacy/")]
        [DataRow("ms-settings://privacy/", true, null, "ms-settings://privacy/")]
        [DataRow("ms-settings:privacy", true, null, "ms-settings:privacy")]
        [DataRow("ms-settings:powersleep", true, null, "ms-settings:powersleep")]
        [DataRow("microsoft-edge:google.com", true, null, "microsoft-edge:google.com")]
        [DataRow("microsoft-edge:google.com/", true, null, "microsoft-edge:google.com/")]
        [DataRow("microsoft-edge:google.com:80/", true, null, "microsoft-edge:google.com:80/")]
        [DataRow("microsoft-edge:google.com:443/", true, null, "microsoft-edge:google.com:443/")]
        [DataRow("microsoft-edge:google.com:1234/", true, null, "microsoft-edge:google.com:1234/")]
        [DataRow("microsoft-edge:http://google.com", true, null, "microsoft-edge:http://google.com")]
        [DataRow("microsoft-edge:http://google.com:80", true, null, "microsoft-edge:http://google.com:80")]
        [DataRow("microsoft-edge:http://google.com:443", true, null, "microsoft-edge:http://google.com:443")]
        [DataRow("microsoft-edge:http://google.com:1234", true, null, "microsoft-edge:http://google.com:1234")]
        [DataRow("microsoft-edge:https://google.com", true, null, "microsoft-edge:https://google.com")]
        [DataRow("microsoft-edge:https://google.com:80", true, null, "microsoft-edge:https://google.com:80")]
        [DataRow("microsoft-edge:https://google.com:443", true, null, "microsoft-edge:https://google.com:443")]
        [DataRow("microsoft-edge:https://google.com:1234", true, null, "microsoft-edge:https://google.com:1234")]
        [DataRow("ftp://user:password@localhost:8080", true, null, "ftp://user:password@localhost:8080/")]
        [DataRow("ftp://user:password@localhost:8080/", true, null, "ftp://user:password@localhost:8080/")]
        [DataRow("ftp://user:password@google.com", true, null, "ftp://user:password@google.com/")]
        [DataRow("ftp://user:password@google.com:2121", true, null, "ftp://user:password@google.com:2121/")]
        [DataRow("ftp://user:password@1.1.1.1", true, null, "ftp://user:password@1.1.1.1/")]
        [DataRow("ftp://user:password@1.1.1.1:2121", true, null, "ftp://user:password@1.1.1.1:2121/")]
        [DataRow("ftp://example.com", true, null, "ftp://example.com/")]
        [DataRow("ftp://example.com/test", true, null, "ftp://example.com/test")]
        [DataRow("ftp://example.com:123/test", true, null, "ftp://example.com:123/test")]
        [DataRow("ftp://example.com/126", true, null, "ftp://example.com/126")]
        [DataRow("^:", false, null, null)]

        public void ParserReturnsExpectedResults(string query, bool expectedSuccess, string expectedWebUri, string expectedSystemUri)
        {
            // Arrange
            var parser = new ExtendedUriParser();

            // Act
            var success = parser.TryParse(query, out var webUriResult, out var systemUriResult);

            // Assert
            Assert.AreEqual(expectedWebUri, webUriResult?.ToString());
            Assert.AreEqual(expectedSystemUri, systemUriResult?.ToString());
            Assert.AreEqual(expectedSuccess, success);
        }
    }
}
