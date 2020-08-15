// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Plugin.Indexer;
using Microsoft.Plugin.Indexer.DriveDetection;
using Microsoft.Plugin.Indexer.SearchHelper;
using Microsoft.Search.Interop;
using Moq;
using NUnit.Framework;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    [TestFixture]
    public class WindowsIndexerTest
    {
        public WindowsSearchAPI GetWindowsSearchAPI()
        {
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query("dummy-connection-string", "dummy-query")).Returns(new List<OleDBResult>());
            return new WindowsSearchAPI(mock.Object);
        }

        [Test]
        public void InitQueryHelper_ShouldInitialize_WhenFunctionIsCalled()
        {
            // Arrange
            int maxCount = 10;
            WindowsSearchAPI api = GetWindowsSearchAPI();
            ISearchQueryHelper queryHelper = null;

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, maxCount, api.DisplayHiddenFiles);

            // Assert
            Assert.IsNotNull(queryHelper);
            Assert.AreEqual(queryHelper.QueryMaxResults, maxCount);
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternIsAsterisk()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "*";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, api.DisplayHiddenFiles);
          
            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternContainsAsterisk()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt*^&)";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternContainsPercent()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt%^&)";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternContainsUnderScore()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt_^&)";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternContainsQuestionMark()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt?^&)";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternDoesNotContainSplSymbols()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt^&)bc";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ExecuteQuery_ShouldDisposeAllConnections_AfterFunctionCall()
        {
            // Arrange
            OleDBSearch oleDbSearch = new OleDBSearch();
            WindowsSearchAPI api = new WindowsSearchAPI(oleDbSearch);

            // Act
            api.Search("FilePath");

            // Assert
            Assert.IsTrue(oleDbSearch.HaveAllDisposableItemsBeenDisposed());
        }

        [Test]
        public void WindowsSearchAPI_ShouldReturnResults_WhenSearchWasExecuted()
        {
            // Arrange
            OleDBResult unHiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file1.txt", "file1.txt" });
            OleDBResult hiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file2.txt", "file2.txt" });
            List<OleDBResult> results = new List<OleDBResult>() { hiddenFile, unHiddenFile };
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query(It.IsAny<string>(), It.IsAny<string>())).Returns(results);
            WindowsSearchAPI api = new WindowsSearchAPI(mock.Object, true);

            // Act
            var windowsSearchAPIResults = api.Search("FilePath");

            // Assert
            Assert.IsTrue(windowsSearchAPIResults.Count() == 2);
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file1.txt"));
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file2.txt"));
        }

        [Test]
        public void WindowsSearchAPI_ShouldNotReturnResultsWithNullValue_WhenDbResultHasANullColumn()
        {
            // Arrange
            OleDBResult unHiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file1.txt", DBNull.Value });
            OleDBResult hiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file2.txt", "file2.txt" });
            List<OleDBResult> results = new List<OleDBResult>() { hiddenFile, unHiddenFile };
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query(It.IsAny<string>(), It.IsAny<string>())).Returns(results);
            WindowsSearchAPI api = new WindowsSearchAPI(mock.Object, false);

            // Act
            var windowsSearchAPIResults = api.Search("FilePath");

            // Assert
            Assert.IsTrue(windowsSearchAPIResults.Count() == 1);
            Assert.IsFalse(windowsSearchAPIResults.Any(x => x.Title == "file1.txt"));
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file2.txt"));
        }

        [Test]
        public void WindowsSearchAPI_ShouldRequestNormalRequest_WhenDisplayHiddenFilesIsTrue()
        {
            ISearchQueryHelper queryHelper;
            String pattern = "notepad";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            _api.DisplayHiddenFiles = true;

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, _api.DisplayHiddenFiles);
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("AND System.FileAttributes <> SOME BITWISE 2"));
        }

        [Test]
        public void WindowsSearchAPI_ShouldRequestFilteredRequest_WhenDisplayHiddenFilesIsFalse()
        {
            ISearchQueryHelper queryHelper;
            String pattern = "notepad";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            _api.DisplayHiddenFiles = false;

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, _api.DisplayHiddenFiles);
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("AND System.FileAttributes <> SOME BITWISE 2"));
        }


        [Test]
        public void WindowsSearchAPI_ShouldRequestNormalRequest_WhenDisplayHiddenFilesIsTrue_AfterRuntimeSwap()
        {
            ISearchQueryHelper queryHelper;
            String pattern = "notepad";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            _api.DisplayHiddenFiles = false;

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, _api.DisplayHiddenFiles);
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);
            _api.DisplayHiddenFiles = true;
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10, _api.DisplayHiddenFiles);
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("AND System.FileAttributes <> SOME BITWISE 2"));
        }

        [TestCase("item.exe")]
        [TestCase("item.bat")]
        [TestCase("item.appref-ms")]
        [TestCase("item.lnk")]
        public void LoadContextMenus_MustLoadAllItems_WhenFileIsAnApp(string path)
        {
            // Arrange
            var mockapi = new Mock<IPublicAPI>();
            var pluginInitContext = new PluginInitContext() { API = mockapi.Object };

            ContextMenuLoader contextMenuLoader = new ContextMenuLoader(pluginInitContext);

            // Act
            Result result = new Result
            {
                ContextData = new SearchResult { Path = path },
            };

            List<ContextMenuResult> contextMenuItems = contextMenuLoader.LoadContextMenus(result);

            // Assert
            Assert.AreEqual(contextMenuItems.Count, 4);
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_copy_path"), Times.Once());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_run_as_administrator"), Times.Once());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_open_containing_folder"), Times.Once());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_open_in_console"), Times.Once());
        }

        [TestCase("item.pdf")]
        [TestCase("item.xls")]
        [TestCase("item.ppt")]
        [TestCase("C:/DummyFile.cs")]
        public void LoadContextMenus_MustNotLoadRunAsAdmin_WhenFileIsAnNotApp(string path)
        {
            // Arrange
            var mockapi = new Mock<IPublicAPI>();
            var pluginInitContext = new PluginInitContext() { API = mockapi.Object };

            ContextMenuLoader contextMenuLoader = new ContextMenuLoader(pluginInitContext);

            // Act
            Result result = new Result
            {
                ContextData = new SearchResult { Path = path },
            };

            List<ContextMenuResult> contextMenuItems = contextMenuLoader.LoadContextMenus(result);

            // Assert
            Assert.AreEqual(contextMenuItems.Count, 3);
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_copy_path"), Times.Once());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_run_as_administrator"), Times.Never());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_open_containing_folder"), Times.Once());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_open_in_console"), Times.Once());
        }

        [TestCase("C:/DummyFolder")]
        [TestCase("TestFolder")]
        public void LoadContextMenus_MustNotLoadRunAsAdminAndOpenContainingFolder_ForFolder(string path)
        {
            // Arrange
            var mockapi = new Mock<IPublicAPI>();
            var pluginInitContext = new PluginInitContext() { API = mockapi.Object };

            ContextMenuLoader contextMenuLoader = new ContextMenuLoader(pluginInitContext);

            // Act
            Result result = new Result
            {
                ContextData = new SearchResult { Path = path },
            };

            List<ContextMenuResult> contextMenuItems = contextMenuLoader.LoadContextMenus(result);

            // Assert
            Assert.AreEqual(contextMenuItems.Count, 2);
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_copy_path"), Times.Once());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_run_as_administrator"), Times.Never());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_open_containing_folder"), Times.Never());
            mockapi.Verify(m => m.GetTranslation("Microsoft_plugin_indexer_open_in_console"), Times.Once());
        }

        [TestCase(0, false, ExpectedResult = true)]
        [TestCase(0, true, ExpectedResult = false)]
        [TestCase(1, false, ExpectedResult = false)]
        [TestCase(1, true, ExpectedResult = false)]
        public bool DriveDetection_MustDisplayWarning_WhenEnhancedModeIsOffAndWhenWarningIsNotDisabled(int enhancedModeStatus, bool disableWarningCheckBoxStatus)
        {
            // Arrange
            var mockRegistry = new Mock<IRegistryWrapper>();
            mockRegistry.Setup(r => r.GetHKLMRegistryValue(It.IsAny<string>(), It.IsAny<string>())).Returns(enhancedModeStatus); // Enhanced mode is disabled

            IndexerDriveDetection driveDetection = new IndexerDriveDetection(mockRegistry.Object);
            driveDetection.IsDriveDetectionWarningCheckBoxSelected = disableWarningCheckBoxStatus;

            // Act & Assert
            return driveDetection.DisplayWarning();
        }

        [Test]
        public void SimplifyQuery_ShouldRemoveLikeQuery_WhenSQLQueryUsesLIKESyntax()
        {
            // Arrange
            string sqlQuery = "SELECT TOP 30 \"System.ItemUrl\", \"System.FileName\", \"System.FileAttributes\" FROM \"SystemIndex\" WHERE (System.FileName LIKE 'abcd.%' OR CONTAINS(System.FileName,'\"abcd.*\"',1033)) AND scope='file:' ORDER BY System.DateModified DESC";

            // Act
            var simplifiedSqlQuery = WindowsSearchAPI.SimplifyQuery(sqlQuery);

            // Assert
            string expectedSqlQuery = "SELECT TOP 30 \"System.ItemUrl\", \"System.FileName\", \"System.FileAttributes\" FROM \"SystemIndex\" WHERE (CONTAINS(System.FileName,'\"abcd.*\"',1033)) AND scope='file:' ORDER BY System.DateModified DESC";
            Assert.IsFalse(simplifiedSqlQuery.Equals(sqlQuery, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsTrue(simplifiedSqlQuery.Equals(expectedSqlQuery, StringComparison.InvariantCultureIgnoreCase));
        }

        [Test]
        public void SimplifyQuery_ShouldReturnArgument_WhenSQLQueryDoesNotUseLIKESyntax()
        {
            // Arrange
            string sqlQuery = "SELECT TOP 30 \"System.ItemUrl\", \"System.FileName\", \"System.FileAttributes\" FROM \"SystemIndex\" WHERE CONTAINS(System.FileName,'\"abcd*\"',1033) AND scope='file:' ORDER BY System.DateModified DESC";

            // Act
            var simplifiedSqlQuery = WindowsSearchAPI.SimplifyQuery(sqlQuery);

            // Assert
            Assert.IsTrue(simplifiedSqlQuery.Equals(sqlQuery, StringComparison.InvariantCultureIgnoreCase));
        }

        [Test]
        public void SimplifyQuery_ShouldRemoveAllOccurrencesOfLikeQuery_WhenSQLQueryUsesLIKESyntaxMultipleTimes()
        {
            // Arrange
            string sqlQuery = "SELECT TOP 30 \"System.ItemUrl\", \"System.FileName\", \"System.FileAttributes\", \"System.FileExtension\" FROM \"SystemIndex\" WHERE (System.FileName LIKE 'ab.%' OR CONTAINS(System.FileName,'\"ab.*\"',1033)) AND (System.FileExtension LIKE '.cd%' OR CONTAINS(System.FileName,'\".cd*\"',1033)) AND scope='file:' ORDER BY System.DateModified DESC";

            // Act
            var simplifiedSqlQuery = WindowsSearchAPI.SimplifyQuery(sqlQuery);

            // Assert
            string expectedSqlQuery = "SELECT TOP 30 \"System.ItemUrl\", \"System.FileName\", \"System.FileAttributes\", \"System.FileExtension\" FROM \"SystemIndex\" WHERE (CONTAINS(System.FileName,'\"ab.*\"',1033)) AND (CONTAINS(System.FileName,'\".cd*\"',1033)) AND scope='file:' ORDER BY System.DateModified DESC";
            Assert.IsFalse(simplifiedSqlQuery.Equals(sqlQuery, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsTrue(simplifiedSqlQuery.Equals(expectedSqlQuery, StringComparison.InvariantCultureIgnoreCase));
        }

        [Test]
        public void SimplifyQuery_ShouldRemoveLikeQuery_WhenSQLQueryUsesLIKESyntaxAndContainsEscapedSingleQuotationMarks()
        {
            // Arrange
            string sqlQuery = "SELECT TOP 30 \"System.ItemUrl\", \"System.FileName\", \"System.FileAttributes\" FROM \"SystemIndex\" WHERE (System.FileName LIKE '''ab.cd''%' OR CONTAINS(System.FileName,'\"'ab.cd'*\"',1033)) AND scope='file:' ORDER BY System.DateModified DESC";

            // Act
            var simplifiedSqlQuery = WindowsSearchAPI.SimplifyQuery(sqlQuery);

            // Assert
            string expectedSqlQuery = "SELECT TOP 30 \"System.ItemUrl\", \"System.FileName\", \"System.FileAttributes\" FROM \"SystemIndex\" WHERE (CONTAINS(System.FileName,'\"'ab.cd'*\"',1033)) AND scope='file:' ORDER BY System.DateModified DESC";
            Assert.IsFalse(simplifiedSqlQuery.Equals(sqlQuery, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsTrue(simplifiedSqlQuery.Equals(expectedSqlQuery, StringComparison.InvariantCultureIgnoreCase));
        }

        [Test]
        public void WindowsSearchAPI_ShouldReturnEmptyResults_WhenIsFullQueryIsTrueAndTheQueryDoesNotRequireLIKESyntax()
        {
            // Arrange
            OleDBResult file1 = new OleDBResult(new List<object>() { "C:/test/path/file1.txt", DBNull.Value });
            OleDBResult file2 = new OleDBResult(new List<object>() { "C:/test/path/file2.txt", "file2.txt" });

            List<OleDBResult> results = new List<OleDBResult>() { file1, file2 };
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query(It.IsAny<string>(), It.IsAny<string>())).Returns(results);
            WindowsSearchAPI _api = new WindowsSearchAPI(mock.Object, false);

            // Act
            var windowsSearchAPIResults = _api.Search("file", true);

            // Assert
            Assert.IsTrue(windowsSearchAPIResults.Count() == 0);
        }
    }
}
