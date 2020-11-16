using System.Linq;
using NUnit.Framework;
using Mono.Collections.Generic;
using Moq;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.Plugin.Sys.UnitTests
{
    public class QueryTests
    {
        [SetUp]
        public void Setup()
        {
	        StringMatcher.Instance = new StringMatcher();
        }

        [TestCase("shutdown", "Shutdown Computer")]
        [TestCase("restart", "Restart Computer")]
        [TestCase("logoff", "Logoff")]
        [TestCase("lock", "Lock this Computer")]
        [TestCase("sleep", "Put computer to sleep")]
        [TestCase("hibernate", "Hibernate computer")]
        [TestCase("empty recycle", "Empty recycle bin")] 
        public void QueryResults(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            string[] terms = { typedString };
            Query expectedQuery = new Query(typedString, typedString, new ReadOnlyCollection<string>(terms), string.Empty);
            // Act

            var result = main.Object.Query(expectedQuery).FirstOrDefault().SubTitle;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }
    }
}