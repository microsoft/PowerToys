// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Mono.Collections.Generic;
using Moq;
using NUnit.Framework;
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
        [TestCase("lock", "Lock Computer")]
        [TestCase("sleep", "Put computer to sleep")]
        [TestCase("hibernate", "Hibernate")]
        [TestCase("empty recycle", "Empty Recycle Bin")]
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
