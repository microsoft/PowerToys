// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Plugin.Folder.Sources.Tests
{
    [TestClass]
    public class FolderHelperTests
    {
        [DataTestMethod]
        [DataRow(@"%SYSTEMDRIVE%", true)]
        [DataRow(@"%O%S", false)]
        [DataRow(@"abcd", false)]
        [DataRow(@"%ProgramData%", true)]
        [DataRow(@"%USERPROFILE%", true)]
        [DataRow(@"powertoys", false)]

        // can't test %HOMEPATH% on CI, so using \User as ExpandEnvironmentVariables was moved up
        [DataRow(@"\Users", true)]
        [DataRow(@"Users", false)]
        public void ExpandsToRootedPath(string query, bool expected)
        {
            var expandedQuery = FolderHelper.Expand(query);
            var result = Path.IsPathRooted(expandedQuery);
            Assert.AreEqual(expected, result);
        }
    }
}
