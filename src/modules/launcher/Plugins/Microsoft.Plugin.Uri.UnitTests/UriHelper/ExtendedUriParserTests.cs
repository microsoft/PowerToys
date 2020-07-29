using Microsoft.Plugin.Uri.UriHelper;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NUnit.Framework;

namespace Microsoft.Plugin.Uri.UnitTests.UriHelper
{
    [TestFixture]
    public class ExtendedUriParserTests
    {
        [TestCase("google.com", true, "http://google.com/")]
        [TestCase("localhost", true, "http://localhost/")]
        [TestCase("127.0.0.1", true, "http://127.0.0.1/")]
        [TestCase("127", true, "http://0.0.0.127/")]
        [TestCase("", false, null)]
        [TestCase("https://google.com", true, "https://google.com/")]
        [TestCase("ftps://google.com", true, "ftps://google.com/")]
        [TestCase(null, false, null)]

        public void TryParse_CanParseHostName(string query, bool expectedSuccess, string expectedResult)
        {
            // Arrange
            var parser = new ExtendedUriParser();

            // Act
            var success = parser.TryParse(query, out var result);

            // Assert
            Assert.AreEqual(expectedSuccess, success);
            Assert.AreEqual(expectedResult, result?.ToString());
        }
    }
}
