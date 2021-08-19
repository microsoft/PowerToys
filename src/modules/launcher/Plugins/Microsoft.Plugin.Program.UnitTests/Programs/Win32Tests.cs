// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Wox.Infrastructure;
using Wox.Infrastructure.FileSystemHelper;
using Wox.Plugin;
using Win32Program = Microsoft.Plugin.Program.Programs.Win32Program;

namespace Microsoft.Plugin.Program.UnitTests.Programs
{
    [TestClass]
    public class Win32Tests
    {
        private static readonly Win32Program _imagingDevices = new Win32Program
        {
            Name = "Imaging Devices",
            ExecutableName = "imagingdevices.exe",
            FullPath = "c:\\program files\\windows photo viewer\\imagingdevices.exe",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _notepadAppdata = new Win32Program
        {
            Name = "Notepad",
            ExecutableName = "notepad.exe",
            FullPath = "c:\\windows\\system32\\notepad.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\accessories\\notepad.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _notepadUsers = new Win32Program
        {
            Name = "Notepad",
            ExecutableName = "notepad.exe",
            FullPath = "c:\\windows\\system32\\notepad.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\accessories\\notepad.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _azureCommandPrompt = new Win32Program
        {
            Name = "Microsoft Azure Command Prompt - v2.9",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            Arguments = @"/E:ON /V:ON /K ""C:\Program Files\Microsoft SDKs\Azure\.NET SDK\v2.9\\bin\setenv.cmd""",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\microsoft azure\\microsoft azure sdk for .net\\v2.9\\microsoft azure command prompt - v2.9.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _visualStudioCommandPrompt = new Win32Program
        {
            Name = "x64 Native Tools Command Prompt for VS 2019",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            Arguments = @"/k ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\VC\Auxiliary\Build\vcvars64.bat""",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\visual studio 2019\\visual studio tools\\vc\\x64 native tools command prompt for vs 2019.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _commandPrompt = new Win32Program
        {
            Name = "Command Prompt",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\system tools\\command prompt.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _fileExplorerLink = new Win32Program
        {
            Name = "File Explorer",
            ExecutableName = "File Explorer.lnk",
            FullPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\system tools\\file explorer.lnk",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _fileExplorer = new Win32Program
        {
            Name = "File Explorer",
            ExecutableName = "explorer.exe",
            FullPath = "c:\\windows\\explorer.exe",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _wordpad = new Win32Program
        {
            Name = "Wordpad",
            ExecutableName = "wordpad.exe",
            FullPath = "c:\\program files\\windows nt\\accessories\\wordpad.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\accessories\\wordpad.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _wordpadDuplicate = new Win32Program
        {
            Name = "WORDPAD",
            ExecutableName = "WORDPAD.EXE",
            FullPath = "c:\\program files\\windows nt\\accessories\\wordpad.exe",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _twitterChromePwa = new Win32Program
        {
            Name = "Twitter",
            FullPath = "c:\\program files (x86)\\google\\chrome\\application\\chrome_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\chrome apps\\twitter.lnk",
            Arguments = " --profile-directory=Default --app-id=jgeosdfsdsgmkedfgdfgdfgbkmhcgcflmi",
            AppType = 0,
        };

        private static readonly Win32Program _pinnedWebpage = new Win32Program
        {
            Name = "Web page",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\web page.lnk",
            Arguments = "--profile-directory=Default --app-id=homljgmgpmcbpjbnjpfijnhipfkiclkd",
            AppType = 0,
        };

        private static readonly Win32Program _edgeNamedPinnedWebpage = new Win32Program
        {
            Name = "edge - Bing",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\edge - bing.lnk",
            Arguments = "  --profile-directory=Default --app-id=aocfnapldcnfbofgmbbllojgocaelgdd",
            AppType = 0,
        };

        private static readonly Win32Program _msedge = new Win32Program
        {
            Name = "Microsoft Edge",
            ExecutableName = "msedge.exe",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\microsoft edge.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _chrome = new Win32Program
        {
            Name = "Google Chrome",
            ExecutableName = "chrome.exe",
            FullPath = "c:\\program files (x86)\\google\\chrome\\application\\chrome.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\google chrome.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _dummyProxyApp = new Win32Program
        {
            Name = "Proxy App",
            ExecutableName = "test_proxy.exe",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\test_proxy.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\test proxy.lnk",
            AppType = Win32Program.ApplicationType.Win32Application,
        };

        private static readonly Win32Program _cmdRunCommand = new Win32Program
        {
            Name = "cmd",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.RunCommand, // Run command
        };

        private static readonly Win32Program _cmderRunCommand = new Win32Program
        {
            Name = "Cmder",
            Description = "Cmder: Lovely Console Emulator",
            ExecutableName = "Cmder.exe",
            FullPath = "c:\\tools\\cmder\\cmder.exe",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.RunCommand, // Run command
        };

        private static readonly Win32Program _dummyInternetShortcutApp = new Win32Program
        {
            Name = "Shop Titans",
            ExecutableName = "Shop Titans.url",
            FullPath = "steam://rungameid/1258080",
            ParentDirectory = "C:\\Users\\temp\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Steam",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.InternetShortcutApplication,
        };

        private static readonly Win32Program _dummyInternetShortcutAppDuplicate = new Win32Program
        {
            Name = "Shop Titans",
            ExecutableName = "Shop Titans.url",
            FullPath = "steam://rungameid/1258080",
            ParentDirectory = "C:\\Users\\temp\\Desktop",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.InternetShortcutApplication,
        };

        private static readonly Win32Program _dummyAppRefApp = new Win32Program
        {
            Name = "Dummy AppRef Application",
            ExecutableName = "dummy.appref-ms",
            FullPath = "C:\\dummy.appref-ms",
            ParentDirectory = "C:\\",
            LnkResolvedPath = null,
            AppType = Win32Program.ApplicationType.ApprefApplication,
        };

        private static readonly Win32Program _dummyShortcutApp = new Win32Program
        {
            Name = "Dummy Shortcut Application",
            ExecutableName = "application.lnk",
            FullPath = "C:\\application.lnk",
            ParentDirectory = "C:\\",
            LnkResolvedPath = "C:\\application.lnk",
            AppType = Win32Program.ApplicationType.ShortcutApplication,
        };

        private static readonly Win32Program _dummyFolderApp = new Win32Program
        {
            Name = "Dummy Folder",
            ExecutableName = "application.lnk",
            FullPath = "C:\\dummy\\folder",
            ParentDirectory = "C:\\dummy\\",
            LnkResolvedPath = "C:\\tools\\application.lnk",
            AppType = Win32Program.ApplicationType.Folder,
        };

        private static readonly Win32Program _dummyGenericFileApp = new Win32Program
        {
            Name = "Dummy Folder",
            ExecutableName = "application.lnk",
            FullPath = "C:\\dummy\\file.pdf",
            ParentDirectory = "C:\\dummy\\",
            LnkResolvedPath = "C:\\tools\\application.lnk",
            AppType = Win32Program.ApplicationType.GenericFile,
        };

        private static IDirectoryWrapper GetMockedDirectoryWrapper()
        {
            var mockDirectory = new Mock<IDirectoryWrapper>();

            // Check if the file has no extension. This is not actually true since there can be files without extensions, but this is sufficient for the purpose of a mock function
            Func<string, bool> returnValue = arg => string.IsNullOrEmpty(System.IO.Path.GetExtension(arg));
            mockDirectory.Setup(m => m.Exists(It.IsAny<string>())).Returns(returnValue);
            return mockDirectory.Object;
        }

        [TestMethod]
        public void DedupFunctionWhenCalledMustRemoveDuplicateNotepads()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>
            {
                _notepadAppdata,
                _notepadUsers,
            };

            // Act
            List<Win32Program> apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(1, apps.Count);
        }

        [TestMethod]
        public void DedupFunctionWhenCalledMustRemoveInternetShortcuts()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>
            {
                _dummyInternetShortcutApp,
                _dummyInternetShortcutAppDuplicate,
            };

            // Act
            List<Win32Program> apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(1, apps.Count);
        }

        [TestMethod]
        public void DedupFunctionWhenCalledMustNotRemovelnkWhichdoesNotHaveExe()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>
            {
                _fileExplorerLink,
            };

            // Act
            List<Win32Program> apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(1, apps.Count);
        }

        [TestMethod]
        public void DedupFunctionMustRemoveDuplicatesForExeExtensionsWithoutLnkResolvedPath()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>
            {
                _wordpad,
                _wordpadDuplicate,
            };

            // Act
            List<Win32Program> apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(1, apps.Count);
            Assert.IsTrue(!string.IsNullOrEmpty(apps[0].LnkResolvedPath));
        }

        [TestMethod]
        public void DedupFunctionMustNotRemoveProgramsWithSameExeNameAndFullPath()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>
            {
                _azureCommandPrompt,
                _visualStudioCommandPrompt,
                _commandPrompt,
            };

            // Act
            List<Win32Program> apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(3, apps.Count);
        }

        [TestMethod]
        public void FunctionIsWebApplicationShouldReturnTrueForWebApplications()
        {
            // The IsWebApplication(() function must return true for all PWAs and pinned web pages
            Assert.IsTrue(_twitterChromePwa.IsWebApplication());
            Assert.IsTrue(_pinnedWebpage.IsWebApplication());
            Assert.IsTrue(_edgeNamedPinnedWebpage.IsWebApplication());

            // Should not filter apps whose executable name ends with proxy.exe
            Assert.IsFalse(_dummyProxyApp.IsWebApplication());
        }

        [DataTestMethod]
        [DataRow("ignore")]
        public void FunctionFilterWebApplicationShouldReturnFalseWhenSearchingForTheMainApp(string query)
        {
            // Irrespective of the query, the FilterWebApplication() Function must not filter main apps such as edge and chrome
            Assert.IsFalse(_msedge.FilterWebApplication(query));
            Assert.IsFalse(_chrome.FilterWebApplication(query));
        }

        [DataTestMethod]
        [DataRow("edge", true)]
        [DataRow("EDGE", true)]
        [DataRow("msedge", true)]
        [DataRow("Microsoft", true)]
        [DataRow("edg", true)]
        [DataRow("Edge page", false)]
        [DataRow("Edge Web page", false)]
        public void EdgeWebSitesShouldBeFilteredWhenSearchingForEdge(string query, bool result)
        {
            Assert.AreEqual(_pinnedWebpage.FilterWebApplication(query), result);
        }

        [DataTestMethod]
        [DataRow("chrome", true)]
        [DataRow("CHROME", true)]
        [DataRow("Google", true)]
        [DataRow("Google Chrome", true)]
        [DataRow("Google Chrome twitter", false)]
        public void ChromeWebSitesShouldBeFilteredWhenSearchingForChrome(string query, bool result)
        {
            Assert.AreEqual(_twitterChromePwa.FilterWebApplication(query), result);
        }

        [DataTestMethod]
        [DataRow("twitter", 0, false)]
        [DataRow("Twit", 0, false)]
        [DataRow("TWITTER", 0, false)]
        [DataRow("web", 1, false)]
        [DataRow("Page", 1, false)]
        [DataRow("WEB PAGE", 1, false)]
        [DataRow("edge", 2, false)]
        [DataRow("EDGE", 2, false)]
        public void PinnedWebPagesShouldNotBeFilteredWhenSearchingForThem(string query, int scenario, bool result)
        {
            const int CASE_TWITTER = 0;
            const int CASE_WEB_PAGE = 1;
            const int CASE_EDGE_NAMED_WEBPAGE = 2;

            // If the query is a part of the name of the web application, it should not be filtered,
            // even if the name is the same as that of the main application, eg: case 2 - edge
            switch (scenario)
            {
                case CASE_TWITTER:
                    Assert.AreEqual(_twitterChromePwa.FilterWebApplication(query), result);
                    return;
                case CASE_WEB_PAGE:
                    Assert.AreEqual(_pinnedWebpage.FilterWebApplication(query), result);
                    return;
                case CASE_EDGE_NAMED_WEBPAGE:
                    Assert.AreEqual(_edgeNamedPinnedWebpage.FilterWebApplication(query), result);
                    return;
                default:
                    break;
            }
        }

        [DataTestMethod]
        [DataRow("Command Prompt")]
        [DataRow("cmd")]
        [DataRow("cmd.exe")]
        [DataRow("ignoreQueryText")]
        public void Win32ApplicationsShouldNotBeFilteredWhenFilteringRunCommands(string query)
        {
            // Even if there is an exact match in the name or exe name, win32 applications should never be filtered
            Assert.IsTrue(_commandPrompt.QueryEqualsNameForRunCommands(query));
        }

        [DataTestMethod]
        [DataRow("explorer")]
        [DataRow("explorer.exe")]
        public void Win32ApplicationsShouldNotFilterWhenExecutingNameOrNameIsUsed(string query)
        {
            // Even if there is an exact match in the name or exe name, win32 applications should never be filtered
            Assert.IsTrue(_fileExplorer.QueryEqualsNameForRunCommands(query));
        }

        [DataTestMethod]
        [DataRow("cmd")]
        [DataRow("Cmd")]
        [DataRow("CMD")]
        public void RunCommandsShouldNotBeFilteredOnExactMatch(string query)
        {
            // Partial matches should be filtered as cmd is not equal to cmder
            Assert.IsFalse(_cmderRunCommand.QueryEqualsNameForRunCommands(query));

            // the query matches the name (cmd) and is therefore not filtered (case-insensitive)
            Assert.IsTrue(_cmdRunCommand.QueryEqualsNameForRunCommands(query));
        }

        [DataTestMethod]
        [DataRow("ımaging")]
        public void Win32ApplicationsShouldNotHaveIncorrectPathWhenExecuting(string query)
        {
            Assert.IsFalse(_imagingDevices.FullPath.Contains(query, StringComparison.Ordinal));
        }

        [TestMethod]
        public void WebApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _pinnedWebpage.ContextMenus(string.Empty, mock.Object);

            // Assert
            Assert.AreEqual(3, contextMenuResults.Count);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_run_as_administrator, contextMenuResults[0].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_containing_folder, contextMenuResults[1].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_in_console, contextMenuResults[2].Title);
        }

        [TestMethod]
        public void InternetShortcutApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _dummyInternetShortcutApp.ContextMenus(string.Empty, mock.Object);

            // Assert
            Assert.AreEqual(2, contextMenuResults.Count);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_containing_folder, contextMenuResults[0].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_in_console, contextMenuResults[1].Title);
        }

        [TestMethod]
        public void Win32ApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _chrome.ContextMenus(string.Empty, mock.Object);

            // Assert
            Assert.AreEqual(3, contextMenuResults.Count);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_run_as_administrator, contextMenuResults[0].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_containing_folder, contextMenuResults[1].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_in_console, contextMenuResults[2].Title);
        }

        [TestMethod]
        public void RunCommandShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _cmdRunCommand.ContextMenus(string.Empty, mock.Object);

            // Assert
            Assert.AreEqual(3, contextMenuResults.Count);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_run_as_administrator, contextMenuResults[0].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_containing_folder, contextMenuResults[1].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_in_console, contextMenuResults[2].Title);
        }

        [TestMethod]
        public void AppRefApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _dummyAppRefApp.ContextMenus(string.Empty, mock.Object);

            // Assert
            Assert.AreEqual(3, contextMenuResults.Count);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_run_as_administrator, contextMenuResults[0].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_containing_folder, contextMenuResults[1].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_in_console, contextMenuResults[2].Title);
        }

        [TestMethod]
        public void ShortcutApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _dummyShortcutApp.ContextMenus(string.Empty, mock.Object);

            // Assert
            Assert.AreEqual(3, contextMenuResults.Count);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_run_as_administrator, contextMenuResults[0].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_containing_folder, contextMenuResults[1].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_in_console, contextMenuResults[2].Title);
        }

        [TestMethod]
        public void FolderApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _dummyFolderApp.ContextMenus(string.Empty, mock.Object);

            // Assert
            Assert.AreEqual(2, contextMenuResults.Count);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_containing_folder, contextMenuResults[0].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_in_console, contextMenuResults[1].Title);
        }

        [TestMethod]
        public void GenericFileApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _dummyGenericFileApp.ContextMenus(string.Empty, mock.Object);

            // Assert
            Assert.AreEqual(2, contextMenuResults.Count);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_containing_folder, contextMenuResults[0].Title);
            Assert.AreEqual(Properties.Resources.wox_plugin_program_open_in_console, contextMenuResults[1].Title);
        }

        [TestMethod]
        public void Win32AppsShouldSetNameAsTitleWhileCreatingResult()
        {
            var mock = new Mock<IPublicAPI>();
            StringMatcher.Instance = new StringMatcher();

            // Act
            var result = _cmderRunCommand.Result("cmder", string.Empty, mock.Object);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(result.Title.Equals(_cmderRunCommand.Name, StringComparison.Ordinal));
            Assert.IsFalse(result.Title.Equals(_cmderRunCommand.Description, StringComparison.Ordinal));
        }

        [DataTestMethod]
        [DataRow("C:\\Program Files\\dummy.exe", Win32Program.ApplicationType.Win32Application)]
        [DataRow("C:\\Program Files\\dummy.msc", Win32Program.ApplicationType.Win32Application)]
        [DataRow("C:\\Program Files\\dummy.lnk", Win32Program.ApplicationType.ShortcutApplication)]
        [DataRow("C:\\Program Files\\dummy.appref-ms", Win32Program.ApplicationType.ApprefApplication)]
        [DataRow("C:\\Program Files\\dummy.url", Win32Program.ApplicationType.InternetShortcutApplication)]
        [DataRow("C:\\Program Files\\dummy", Win32Program.ApplicationType.Folder)]
        [DataRow("C:\\Program Files\\dummy.txt", Win32Program.ApplicationType.GenericFile)]
        public void GetAppTypeFromPathShouldReturnCorrectAppTypeWhenAppPathIsPassedAsArgument(string path, Win32Program.ApplicationType result)
        {
            // Directory.Exists must be mocked
            Win32Program.DirectoryWrapper = GetMockedDirectoryWrapper();

            // Act
            Assert.AreEqual(Win32Program.GetAppTypeFromPath(path), result);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("ping 1.1.1.1")]
        public void EmptyArgumentsShouldNotThrow(string argument)
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();

            // Act
            List<ContextMenuResult> contextMenuResults = _dummyInternetShortcutApp.ContextMenus(argument, mock.Object);

            // Assert (Should always return if the above does not throw any exception)
            Assert.IsTrue(true);
        }
    }
}
