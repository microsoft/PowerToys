// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using Microsoft.Search.Interop;
using Microsoft.Plugin.Indexer.SearchHelper;
using Microsoft.Plugin.Indexer;
using Moq;
using Wox.Plugin;
using System.Linq;
using Microsoft.Plugin.Indexer.DriveDetection;

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
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            ISearchQueryHelper queryHelper = null;

            // Act
            WindowsSearchAPI.InitQueryHelper(out queryHelper, maxCount);

            // Assert
            Assert.IsNotNull(queryHelper);
            Assert.AreEqual(queryHelper.QueryMaxResults, maxCount);
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternIsAsterisk()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            String pattern = "*";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10);

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
            String pattern = "tt*^&)";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10);

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
            String pattern = "tt%^&)";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10);

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
            String pattern = "tt_^&)";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10);

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
            String pattern = "tt?^&)";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10);

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
            String pattern = "tt^&)bc";
            WindowsSearchAPI _api = GetWindowsSearchAPI();
            WindowsSearchAPI.InitQueryHelper(out queryHelper, 10);

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
            WindowsSearchAPI _api = new WindowsSearchAPI(oleDbSearch);

            // Act
            _api.Search("FilePath");

            // Assert
            Assert.IsTrue(oleDbSearch.HaveAllDisposableItemsBeenDisposed());
        }

        [Test]
        public void WindowsSearchAPI_ShouldShowHiddenFiles_WhenDisplayHiddenFilesIsTrue()
        {
            // Arrange
            OleDBResult unHiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file1.txt", "file1.txt", (Int64)0x0 });
            OleDBResult hiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file2.txt", "file2.txt", (Int64)0x2 });
            List<OleDBResult> results = new List<OleDBResult>() { hiddenFile, unHiddenFile };
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query(It.IsAny<string>(), It.IsAny<string>())).Returns(results);
            WindowsSearchAPI _api = new WindowsSearchAPI(mock.Object, true);

            // Act
            var windowsSearchAPIResults = _api.Search("FilePath");

            // Assert
            Assert.IsTrue(windowsSearchAPIResults.Count() == 2);
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file1.txt"));
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file2.txt"));
        }

        [Test]
        public void WindowsSearchAPI_ShouldNotShowHiddenFiles_WhenDisplayHiddenFilesIsFalse()
        {
            // Arrange
            OleDBResult unHiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file1.txt", "file1.txt", (Int64)0x0 });
            OleDBResult hiddenFile = new OleDBResult(new List<object>() { "C:/test/path/file2.txt", "file2.txt", (Int64)0x2 });
            List<OleDBResult> results = new List<OleDBResult>() { hiddenFile, unHiddenFile };
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query(It.IsAny<string>(), It.IsAny<string>())).Returns(results);
            WindowsSearchAPI _api = new WindowsSearchAPI(mock.Object, false);

            // Act
            var windowsSearchAPIResults = _api.Search("FilePath");

            // Assert
            Assert.IsTrue(windowsSearchAPIResults.Count() == 1);
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file1.txt"));
            Assert.IsFalse(windowsSearchAPIResults.Any(x => x.Title == "file2.txt"));
        }

        [Test]
        public void WindowsSearchAPI_ShouldNotReturnResultsWithNullValue_WhenDbResultHasANullColumn()
        {
            // Arrange
            OleDBResult file1 = new OleDBResult(new List<object>() { "C:/test/path/file1.txt", DBNull.Value, (Int64)0x0 });
            OleDBResult file2 = new OleDBResult(new List<object>() { "C:/test/path/file2.txt", "file2.txt", (Int64)0x0 });

            List<OleDBResult> results = new List<OleDBResult>() { file1, file2 };
            var mock = new Mock<ISearch>();
            mock.Setup(x => x.Query(It.IsAny<string>(), It.IsAny<string>())).Returns(results);
            WindowsSearchAPI _api = new WindowsSearchAPI(mock.Object, false);

            // Act
            var windowsSearchAPIResults = _api.Search("FilePath");

            // Assert
            Assert.IsTrue(windowsSearchAPIResults.Count() == 1);
            Assert.IsFalse(windowsSearchAPIResults.Any(x => x.Title == "file1.txt"));
            Assert.IsTrue(windowsSearchAPIResults.Any(x => x.Title == "file2.txt"));
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

            ContextMenuLoader _contextMenuLoader = new ContextMenuLoader(pluginInitContext);

            // Act
            Result result = new Result
            {
                ContextData = new SearchResult { Path = path }
            };

            List<ContextMenuResult> contextMenuItems = _contextMenuLoader.LoadContextMenus(result);

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

            ContextMenuLoader _contextMenuLoader = new ContextMenuLoader(pluginInitContext);

            // Act
            Result result = new Result
            {
                ContextData = new SearchResult { Path = path }
            };

            List<ContextMenuResult> contextMenuItems = _contextMenuLoader.LoadContextMenus(result);

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

            ContextMenuLoader _contextMenuLoader = new ContextMenuLoader(pluginInitContext);

            // Act
            Result result = new Result
            {
                ContextData = new SearchResult { Path = path }
            };

            List<ContextMenuResult> contextMenuItems = _contextMenuLoader.LoadContextMenus(result);

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

            IndexerDriveDetection _driveDetection = new IndexerDriveDetection(mockRegistry.Object);
            _driveDetection.IsDriveDetectionWarningCheckBoxSelected = disableWarningCheckBoxStatus;

            // Act & Assert
            return _driveDetection.DisplayWarning();
        }
    }
}
