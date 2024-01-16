// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.Plugin.Folder.UnitTests
{
    [TestClass]
    public class QueryEnvironmentVariableTests
    {
        private static QueryEnvironmentVariable _queryEnvironmentVariable;
        private static MockFileSystem _fileSystem;

        [TestInitialize]
        public void SetupMock()
        {
            var environmentHelperMock = new Mock<IEnvironmentHelper>();

            environmentHelperMock
                .Setup(h => h.GetEnvironmentVariables())
                .Returns(() => new Dictionary<string, string>
                {
                    { "OS", "Windows_NT" },
                    { "WINDIR", @"C:\Windows" },
                    { "PROGRAMDATA", @"C:\ProgramData" },
                    { "PROGRAMFILES", @"C:\Program Files" },
                });

            _fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                { @"C:\Windows", new MockDirectoryData() },
                { @"C:\ProgramData", new MockDirectoryData() },
                { @"C:\Program Files", new MockDirectoryData() },
            });

            _queryEnvironmentVariable = new QueryEnvironmentVariable(_fileSystem.Directory, environmentHelperMock.Object);
        }

        [DataTestMethod]
        [DataRow(@"%OS%")] // Not a directory
        [DataRow(@"%CUSTOM%")] // Directory doesn't exist
        [DataRow(@"WINDIR")] // Not an environment variable
        public void QueryWithEmptyResults(string search)
        {
            var results = _queryEnvironmentVariable.Query(search);
            Assert.AreEqual(results.Count(), 0);
        }

        [DataTestMethod]
        [DataRow(@"", 3)]
        [DataRow(@"%", 3)]
        [DataRow(@"%WIN", 1)]
        [DataRow(@"%WINDIR%", 1)]
        [DataRow(@"%PROGRAM", 2)]
        public void QueryWithResults(string search, int numberOfResults)
        {
            var results = _queryEnvironmentVariable.Query(search);
            Assert.AreEqual(results.Count(), numberOfResults);
        }
    }
}
