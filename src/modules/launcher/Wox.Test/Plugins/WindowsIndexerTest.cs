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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    [TestClass]
    public class WindowsIndexerTest
    {
        private static WindowsSearchAPI GetWindowsSearchAPI()
        {
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query("dummy-connection-string", "dummy-query")).Returns(new List<OleDBResult>());
            return new WindowsSearchAPI(mock.Object);
        }

        private static ISearchManager GetMockSearchManager()
        {
            var sqlQuery = "SELECT TOP 30 \"System.ItemUrl\", \"System.FileName\", \"System.FileAttributes\" FROM \"SystemIndex\" WHERE CONTAINS(System.FileName,'\"FilePath\"',1033) AND scope='file:' ORDER BY System.DateModified DESC";
            var mockSearchManager = new Mock<ISearchManager>();
            var mockCatalog = new Mock<CSearchCatalogManager>();
            var mockQueryHelper = new Mock<CSearchQueryHelper>();
            mockQueryHelper.SetupAllProperties();
            mockQueryHelper.Setup(x => x.ConnectionString).Returns("provider=Search.CollatorDSO.1;EXTENDED PROPERTIES=\"Application=Windows\"");
            mockQueryHelper.Setup(x => x.GenerateSQLFromUserQuery(It.IsAny<string>())).Returns(sqlQuery);
            mockSearchManager.Setup(x => x.GetCatalog(It.IsAny<string>())).Returns(mockCatalog.Object);
            mockCatalog.Setup(x => x.GetQueryHelper()).Returns(mockQueryHelper.Object);
            return mockSearchManager.Object;
        }

        [TestMethod]
        public void InitQueryHelperShouldInitializeWhenFunctionIsCalled()
        {
            // Arrange
            int maxCount = 10;
            WindowsSearchAPI api = GetWindowsSearchAPI();
            ISearchQueryHelper queryHelper = null;
            var mockSearchManager = GetMockSearchManager();

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, maxCount, api.DisplayHiddenFiles);

            // Assert
            Assert.IsNotNull(queryHelper);
            Assert.AreEqual(maxCount, queryHelper.QueryMaxResults);
        }

        [TestMethod]
        public void ModifyQueryHelperShouldSetQueryHelperWhenPatternIsAsterisk()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "*";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            var mockSearchManager = GetMockSearchManager();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("LIKE", StringComparison.Ordinal));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ModifyQueryHelperShouldSetQueryHelperWhenPatternContainsAsterisk()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt*^&)";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            var mockSearchManager = GetMockSearchManager();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE", StringComparison.Ordinal));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ModifyQueryHelperShouldSetQueryHelperWhenPatternContainsPercent()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt%^&)";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            var mockSearchManager = GetMockSearchManager();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE", StringComparison.Ordinal));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ModifyQueryHelperShouldSetQueryHelperWhenPatternContainsUnderScore()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt_^&)";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            var mockSearchManager = GetMockSearchManager();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE", StringComparison.Ordinal));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ModifyQueryHelperShouldSetQueryHelperWhenPatternContainsQuestionMark()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt?^&)";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            var mockSearchManager = GetMockSearchManager();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE", StringComparison.Ordinal));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ModifyQueryHelperShouldSetQueryHelperWhenPatternDoesNotContainSplSymbols()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            string pattern = "tt^&)bc";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            var mockSearchManager = GetMockSearchManager();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);

            // Act
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("LIKE", StringComparison.Ordinal));
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("Contains", StringComparison.Ordinal));
        }

        [TestMethod]
        public void WindowsSearchAPIShouldReturnResultsWhenSearchWasExecuted()
        {
            // Arrange
            OleDBResult unHiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file1.txt", "file1.txt" });
            OleDBResult hiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file2.txt", "file2.txt" });
            List<OleDBResult> results = new List<OleDBResult>() { hiddenFile, unHiddenFile };
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query(It.IsAny<string>(), It.IsAny<string>())).Returns(results);
            WindowsSearchAPI api = new WindowsSearchAPI(mock.Object, true);
            var mockSearchManager = GetMockSearchManager();

            // Act
            var windowsSearchAPIResults = api.Search("FilePath", mockSearchManager);

            // Assert
            Assert.IsTrue(windowsSearchAPIResults.Count() == 2);
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file1.txt"));
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file2.txt"));
        }

        [TestMethod]
        public void WindowsSearchAPIShouldNotReturnResultsWithNullValueWhenDbResultHasANullColumn()
        {
            // Arrange
            OleDBResult unHiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file1.txt", DBNull.Value });
            OleDBResult hiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file2.txt", "file2.txt" });
            List<OleDBResult> results = new List<OleDBResult>() { hiddenFile, unHiddenFile };
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query(It.IsAny<string>(), It.IsAny<string>())).Returns(results);
            WindowsSearchAPI api = new WindowsSearchAPI(mock.Object, false);
            var mockSearchManager = GetMockSearchManager();

            // Act
            var windowsSearchAPIResults = api.Search("FilePath", mockSearchManager);

            // Assert
            Assert.IsTrue(windowsSearchAPIResults.Count() == 1);
            Assert.IsFalse(windowsSearchAPIResults.Any(x => x.Title == "file1.txt"));
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file2.txt"));
        }

        [TestMethod]
        public void WindowsSearchAPIShouldRequestNormalRequestWhenDisplayHiddenFilesIsTrue()
        {
            ISearchQueryHelper queryHelper;
            string pattern = "notepad";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            api.DisplayHiddenFiles = true;
            var mockSearchManager = GetMockSearchManager();

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("AND System.FileAttributes <> SOME BITWISE 2", StringComparison.Ordinal));
        }

        [TestMethod]
        public void WindowsSearchAPIShouldRequestFilteredRequestWhenDisplayHiddenFilesIsFalse()
        {
            ISearchQueryHelper queryHelper;
            string pattern = "notepad";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            api.DisplayHiddenFiles = false;
            var mockSearchManager = GetMockSearchManager();

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("AND System.FileAttributes <> SOME BITWISE 2", StringComparison.Ordinal));
        }

        [TestMethod]
        public void WindowsSearchAPIShouldRequestNormalRequestWhenDisplayHiddenFilesIsTrueAfterRuntimeSwap()
        {
            ISearchQueryHelper queryHelper;
            string pattern = "notepad";
            WindowsSearchAPI api = GetWindowsSearchAPI();
            api.DisplayHiddenFiles = false;
            var mockSearchManager = GetMockSearchManager();

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);
            api.DisplayHiddenFiles = true;
            WindowsSearchAPI.InitQueryHelper(out queryHelper, mockSearchManager, 10, api.DisplayHiddenFiles);
            WindowsSearchAPI.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("AND System.FileAttributes <> SOME BITWISE 2", StringComparison.Ordinal));
        }

        [DataTestMethod]
        [DataRow("item.exe")]
        [DataRow("item.bat")]
        [DataRow("item.appref-ms")]
        [DataRow("item.lnk")]
        public void LoadContextMenusMustLoadAllItemsWhenFileIsAnApp(string path)
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
            Assert.AreEqual(4, contextMenuItems.Count);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_open_containing_folder, contextMenuItems[0].Title);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_run_as_administrator, contextMenuItems[1].Title);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_copy_path, contextMenuItems[2].Title);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_open_in_console, contextMenuItems[3].Title);
        }

        [DataTestMethod]
        [DataRow("item.pdf")]
        [DataRow("item.xls")]
        [DataRow("item.ppt")]
        [DataRow("C:/DummyFile.cs")]
        public void LoadContextMenusMustNotLoadRunAsAdminWhenFileIsAnNotApp(string path)
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
            Assert.AreEqual(3, contextMenuItems.Count);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_open_containing_folder, contextMenuItems[0].Title);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_copy_path, contextMenuItems[1].Title);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_open_in_console, contextMenuItems[2].Title);
        }

        [DataTestMethod]
        [DataRow("C:/DummyFolder")]
        [DataRow("TestFolder")]
        public void LoadContextMenusMustNotLoadRunAsAdminAndOpenContainingFolderForFolder(string path)
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
            Assert.AreEqual(2, contextMenuItems.Count);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_copy_path, contextMenuItems[0].Title);
            Assert.AreEqual(Microsoft.Plugin.Indexer.Properties.Resources.Microsoft_plugin_indexer_open_in_console, contextMenuItems[1].Title);
        }

        [DataTestMethod]
        [DataRow(0, 2, false, true)]
        [DataRow(0, 3, true, false)]
        [DataRow(1, 2, false, false)]
        [DataRow(1, 4, true, false)]
        [DataRow(0, 1, false, false)]
        public void DriveDetectionMustDisplayWarningWhenEnhancedModeIsOffAndWhenWarningIsNotDisabled(int enhancedModeStatus, int driveCount, bool disableWarningCheckBoxStatus, bool result)
        {
            // Arrange
            var mockRegistry = new Mock<IRegistryWrapper>();
            mockRegistry.Setup(r => r.GetHKLMRegistryValue(It.IsAny<string>(), It.IsAny<string>())).Returns(enhancedModeStatus); // Enhanced mode is disabled

            var mockDriveInfo = new Mock<IDriveInfoWrapper>();
            mockDriveInfo.Setup(d => d.GetDriveCount()).Returns(driveCount);

            IndexerDriveDetection driveDetection = new IndexerDriveDetection(mockRegistry.Object, mockDriveInfo.Object);
            driveDetection.IsDriveDetectionWarningCheckBoxSelected = disableWarningCheckBoxStatus;

            // Act & Assert
            Assert.AreEqual(driveDetection.DisplayWarning(), result);
        }
    }
}
