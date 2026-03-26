// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using PowerToys.FileLocksmithUI.Helpers;

namespace PowerToys.FileLocksmithUI.UnitTests.Helpers
{
    [TestClass]
    public class CliArgHelperTests
    {
        [TestMethod]
        public void GetPathsFromArgs_WithFilePaths_ReturnsPaths()
        {
            string[] args = [@"C:\App.exe", @"C:\Temp", @"C:\file.txt"];
            string[] result = CliArgHelper.GetPathsFromArgs(args);
            CollectionAssert.AreEqual(new[] { @"C:\Temp", @"C:\file.txt" }, result);
        }

        [TestMethod]
        public void GetPathsFromArgs_WithOnlyExePath_ReturnsEmpty()
        {
            string[] args = [@"C:\App.exe"];
            string[] result = CliArgHelper.GetPathsFromArgs(args);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void GetPathsFromArgs_WithElevatedFlagAndPath_FiltersFlag()
        {
            string[] args = [@"C:\App.exe", "--elevated", @"C:\Temp"];
            string[] result = CliArgHelper.GetPathsFromArgs(args);
            CollectionAssert.AreEqual(new[] { @"C:\Temp" }, result);
        }

        [TestMethod]
        public void GetPathsFromArgs_WithElevatedFlagAndMultiplePaths_FiltersFlag()
        {
            string[] args = [@"C:\App.exe", "--elevated", @"C:\Temp", @"C:\file.txt"];
            string[] result = CliArgHelper.GetPathsFromArgs(args);
            CollectionAssert.AreEqual(new[] { @"C:\Temp", @"C:\file.txt" }, result);
        }

        [TestMethod]
        [DataRow("--ELEVATED")]
        [DataRow("--Elevated")]
        [DataRow("--elevated")]
        public void GetPathsFromArgs_ElevatedFlagIsCaseInsensitive(string elevatedFlag)
        {
            string[] args = [@"C:\App.exe", elevatedFlag, @"C:\Temp"];
            string[] result = CliArgHelper.GetPathsFromArgs(args);
            CollectionAssert.AreEqual(new[] { @"C:\Temp" }, result);
        }

        [TestMethod]
        public void GetPathsFromArgs_PathStartingWithDoubleDash_TreatsAsFilePath()
        {
            // A path that starts with -- but is not --elevated should be treated as a file path
            string[] args = [@"C:\App.exe", @"--some-file.txt"];
            string[] result = CliArgHelper.GetPathsFromArgs(args);
            CollectionAssert.AreEqual(new[] { @"--some-file.txt" }, result);
        }
    }
}
