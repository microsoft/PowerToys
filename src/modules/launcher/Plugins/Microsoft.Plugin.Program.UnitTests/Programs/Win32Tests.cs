using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Wox.Infrastructure;
using Wox.Plugin;

using Microsoft.Plugin.Program;
using System.IO.Packaging;
using Windows.ApplicationModel;
namespace Microsoft.Plugin.Program.UnitTests.Programs
{
    using Win32Program = Microsoft.Plugin.Program.Programs.Win32Program;

    [TestFixture]
    public class Win32Tests
    {
        static Win32Program notepad_appdata = new Win32Program
        {
            Name = "Notepad",
            ExecutableName = "notepad.exe",
            FullPath = "c:\\windows\\system32\\notepad.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\accessories\\notepad.lnk",
            AppType = 2
        };

        static Win32Program notepad_users = new Win32Program
        {
            Name = "Notepad",
            ExecutableName = "notepad.exe",
            FullPath = "c:\\windows\\system32\\notepad.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\accessories\\notepad.lnk",
            AppType = 2
        };

        static Win32Program azure_command_prompt = new Win32Program
        {
            Name = "Microsoft Azure Command Prompt - v2.9",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\microsoft azure\\microsoft azure sdk for .net\\v2.9\\microsoft azure command prompt - v2.9.lnk",
            AppType = 2
        };

        static Win32Program visual_studio_command_prompt = new Win32Program
        {
            Name = "x64 Native Tools Command Prompt for VS 2019",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\visual studio 2019\\visual studio tools\\vc\\x64 native tools command prompt for vs 2019.lnk",
            AppType = 2
        };

        static Win32Program command_prompt = new Win32Program
        {
            Name = "Command Prompt",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\system tools\\command prompt.lnk",
            AppType = 2
        };

        static Win32Program file_explorer = new Win32Program
        {
            Name = "File Explorer",
            ExecutableName = "File Explorer.lnk",
            FullPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\system tools\\file explorer.lnk",
            LnkResolvedPath = null,
            AppType = 2
        };

        static Win32Program wordpad = new Win32Program
        {
            Name = "Wordpad",
            ExecutableName = "wordpad.exe",
            FullPath = "c:\\program files\\windows nt\\accessories\\wordpad.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\accessories\\wordpad.lnk",
            AppType = 2
        };

        static Win32Program wordpad_duplicate = new Win32Program
        {
            Name = "WORDPAD",
            ExecutableName = "WORDPAD.EXE",
            FullPath = "c:\\program files\\windows nt\\accessories\\wordpad.exe",
            LnkResolvedPath = null,
            AppType = 2
        };

        static Win32Program twitter_pwa = new Win32Program
        {
            Name = "Twitter",
            FullPath = "c:\\program files (x86)\\google\\chrome\\application\\chrome_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\chrome apps\\twitter.lnk",
            Arguments = " --profile-directory=Default --app-id=jgeosdfsdsgmkedfgdfgdfgbkmhcgcflmi",
            AppType = 0
        };

        static Win32Program pinned_webpage = new Win32Program
        {
            Name = "Web page",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\web page.lnk",
            Arguments = "--profile-directory=Default --app-id=homljgmgpmcbpjbnjpfijnhipfkiclkd",
            AppType = 0
        };

        static Win32Program edge_named_pinned_webpage = new Win32Program
        {
            Name = "edge - Bing",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\edge - bing.lnk",
            Arguments = "  --profile-directory=Default --app-id=aocfnapldcnfbofgmbbllojgocaelgdd",
            AppType = 0
        };

        static Win32Program msedge = new Win32Program
        {
            Name = "Microsoft Edge",
            ExecutableName = "msedge.exe",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\microsoft edge.lnk",
            AppType = 2
        };

        static Win32Program chrome = new Win32Program
        {
            Name = "Google Chrome",
            ExecutableName = "chrome.exe",
            FullPath = "c:\\program files (x86)\\google\\chrome\\application\\chrome.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\google chrome.lnk",
            AppType = 2
        };

        static Win32Program dummy_proxy_app = new Win32Program
        {
            Name = "Proxy App",
            ExecutableName = "test_proxy.exe",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\test_proxy.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\test proxy.lnk",
            AppType = 2
        };

        static Win32Program cmd_run_command = new Win32Program
        {
            Name = "cmd",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = null,
            AppType = 3 // Run command
        };

        static Win32Program cmder_run_command = new Win32Program
        {
            Name = "Cmder",
            Description = "Cmder: Lovely Console Emulator",
            ExecutableName = "Cmder.exe",
            FullPath = "c:\\tools\\cmder\\cmder.exe",
            LnkResolvedPath = null,
            AppType = 3 // Run command
        };

        static Win32Program dummy_internetShortcut_app = new Win32Program
        {
            Name = "Shop Titans",
            ExecutableName = "Shop Titans.url",
            FullPath = "steam://rungameid/1258080",
            ParentDirectory = "C:\\Users\\temp\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Steam",
            LnkResolvedPath = null,
            AppType = 1
        };

        static Win32Program dummy_internetShortcut_app_duplicate = new Win32Program
        {
            Name = "Shop Titans",
            ExecutableName = "Shop Titans.url",
            FullPath = "steam://rungameid/1258080",
            ParentDirectory = "C:\\Users\\temp\\Desktop",
            LnkResolvedPath = null,
            AppType = 1
        };

        [Test]
        public void DedupFunctionWhenCalledMustRemoveDuplicateNotepads()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>();
            prgms.Add(notepad_appdata);
            prgms.Add(notepad_users);

            // Act
            Win32Program[] apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 1);
        }

        [Test]
        public void DedupFunctionWhenCalledMustRemoveInternetShortcuts()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>();
            prgms.Add(dummy_internetShortcut_app);
            prgms.Add(dummy_internetShortcut_app_duplicate);

            // Act
            Win32Program[] apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 1);
        }

        [Test]
        public void DedupFunctionWhenCalledMustNotRemovelnkWhichdoesNotHaveExe()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>();
            prgms.Add(file_explorer);

            // Act
            Win32Program[] apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 1);
        }

        [Test]
        public void DedupFunctionMustRemoveDuplicatesForExeExtensionsWithoutLnkResolvedPath()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>();
            prgms.Add(wordpad);
            prgms.Add(wordpad_duplicate);

            // Act
            Win32Program[] apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 1);
            Assert.IsTrue(!string.IsNullOrEmpty(apps[0].LnkResolvedPath));
        }

        [Test]
        public void DedupFunctionMustNotRemoveProgramsWithSameExeNameAndFullPath()
        {
            // Arrange
            List<Win32Program> prgms = new List<Win32Program>();
            prgms.Add(azure_command_prompt);
            prgms.Add(visual_studio_command_prompt);
            prgms.Add(command_prompt);

            // Act
            Win32Program[] apps = Win32Program.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 3);
        }

        [Test]
        public void FunctionIsWebApplicationShouldReturnTrueForWebApplications()
        {
            // The IsWebApplication(() function must return true for all PWAs and pinned web pages
            Assert.IsTrue(twitter_pwa.IsWebApplication());
            Assert.IsTrue(pinned_webpage.IsWebApplication());
            Assert.IsTrue(edge_named_pinned_webpage.IsWebApplication());

            // Should not filter apps whose executable name ends with proxy.exe
            Assert.IsFalse(dummy_proxy_app.IsWebApplication());
        }

        [TestCase("ignore")]
        public void FunctionFilterWebApplicationShouldReturnFalseWhenSearchingForTheMainApp(string query)
        {
            // Irrespective of the query, the FilterWebApplication() Function must not filter main apps such as edge and chrome
            Assert.IsFalse(msedge.FilterWebApplication(query));
            Assert.IsFalse(chrome.FilterWebApplication(query));
        }

        [TestCase("edge", ExpectedResult = true)]
        [TestCase("EDGE", ExpectedResult = true)]
        [TestCase("msedge", ExpectedResult = true)]
        [TestCase("Microsoft", ExpectedResult = true)]
        [TestCase("edg", ExpectedResult = true)]
        [TestCase("Edge page", ExpectedResult = false)]
        [TestCase("Edge Web page", ExpectedResult = false)]
        public bool EdgeWebSitesShouldBeFilteredWhenSearchingForEdge(string query)
        {
            return pinned_webpage.FilterWebApplication(query);
        }

        [TestCase("chrome", ExpectedResult = true)]
        [TestCase("CHROME", ExpectedResult = true)]
        [TestCase("Google", ExpectedResult = true)]
        [TestCase("Google Chrome", ExpectedResult = true)]
        [TestCase("Google Chrome twitter", ExpectedResult = false)]
        public bool ChromeWebSitesShouldBeFilteredWhenSearchingForChrome(string query)
        {
            return twitter_pwa.FilterWebApplication(query);
        }

        [TestCase("twitter", 0, ExpectedResult = false)]
        [TestCase("Twit", 0, ExpectedResult = false)]
        [TestCase("TWITTER", 0, ExpectedResult = false)]
        [TestCase("web", 1, ExpectedResult = false)]
        [TestCase("Page", 1, ExpectedResult = false)]
        [TestCase("WEB PAGE", 1, ExpectedResult = false)]
        [TestCase("edge", 2, ExpectedResult = false)]
        [TestCase("EDGE", 2, ExpectedResult = false)]
        public bool PinnedWebPagesShouldNotBeFilteredWhenSearchingForThem(string query, int Case)
        {
            const uint CASE_TWITTER = 0;
            const uint CASE_WEB_PAGE = 1;
            const uint CASE_EDGE_NAMED_WEBPAGE = 2;

            // If the query is a part of the name of the web application, it should not be filtered,
            // even if the name is the same as that of the main application, eg: case 2 - edge
            if (Case == CASE_TWITTER)
            {
                return twitter_pwa.FilterWebApplication(query);
            }
            else if (Case == CASE_WEB_PAGE)
            {
                return pinned_webpage.FilterWebApplication(query);
            }
            else if (Case == CASE_EDGE_NAMED_WEBPAGE)
            {
                return edge_named_pinned_webpage.FilterWebApplication(query);
            }
            // unreachable code
            return true;
        }

        [TestCase("Command Prompt")]
        [TestCase("cmd")]
        [TestCase("cmd.exe")]
        [TestCase("ignoreQueryText")]
        public void Win32ApplicationsShouldNotBeFilteredWhenFilteringRunCommands(string query)
        {
            // Even if there is an exact match in the name or exe name, win32 applications should never be filtered
            Assert.IsTrue(command_prompt.QueryEqualsNameForRunCommands(query));
        }

        [TestCase("cmd")]
        [TestCase("Cmd")]
        [TestCase("CMD")]
        public void RunCommandsShouldNotBeFilteredOnExactMatch(string query)
        {
            // Partial matches should be filtered as cmd is not equal to cmder
            Assert.IsFalse(cmder_run_command.QueryEqualsNameForRunCommands(query));

            // the query matches the name (cmd) and is therefore not filtered (case-insensitive)
            Assert.IsTrue(cmd_run_command.QueryEqualsNameForRunCommands(query));
        }

        [Test]
        public void WebApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();
            mock.Setup(x => x.GetTranslation(It.IsAny<string>())).Returns(It.IsAny<string>());

            // Act
            List<ContextMenuResult> contextMenuResults = pinned_webpage.ContextMenus(mock.Object);

            // Assert
            Assert.AreEqual(contextMenuResults.Count, 3);
            mock.Verify(x => x.GetTranslation("wox_plugin_program_run_as_administrator"), Times.Once());
            mock.Verify(x => x.GetTranslation("wox_plugin_program_open_containing_folder"), Times.Once());
            mock.Verify(x => x.GetTranslation("wox_plugin_program_open_in_console"), Times.Once());
        }

        [Test]
        public void InternetShortcutApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();
            mock.Setup(x => x.GetTranslation(It.IsAny<string>())).Returns(It.IsAny<string>());

            // Act
            List<ContextMenuResult> contextMenuResults = dummy_internetShortcut_app.ContextMenus(mock.Object);

            // Assert
            Assert.AreEqual(contextMenuResults.Count, 2);
            mock.Verify(x => x.GetTranslation("wox_plugin_program_open_containing_folder"), Times.Once());
            mock.Verify(x => x.GetTranslation("wox_plugin_program_open_in_console"), Times.Once());
        }

        [Test]
        public void Win32ApplicationShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();
            mock.Setup(x => x.GetTranslation(It.IsAny<string>())).Returns(It.IsAny<string>());

            // Act
            List<ContextMenuResult> contextMenuResults = chrome.ContextMenus(mock.Object);

            // Assert
            Assert.AreEqual(contextMenuResults.Count, 3);
            mock.Verify(x => x.GetTranslation("wox_plugin_program_run_as_administrator"), Times.Once());
            mock.Verify(x => x.GetTranslation("wox_plugin_program_open_containing_folder"), Times.Once());
            mock.Verify(x => x.GetTranslation("wox_plugin_program_open_in_console"), Times.Once());
        }

        [Test]
        public void RunCommandShouldReturnContextMenuWithOpenInConsoleWhenContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();
            mock.Setup(x => x.GetTranslation(It.IsAny<string>())).Returns(It.IsAny<string>());

            // Act
            List<ContextMenuResult> contextMenuResults = cmd_run_command.ContextMenus(mock.Object);

            // Assert
            Assert.AreEqual(contextMenuResults.Count, 3);
            mock.Verify(x => x.GetTranslation("wox_plugin_program_run_as_administrator"), Times.Once());
            mock.Verify(x => x.GetTranslation("wox_plugin_program_open_containing_folder"), Times.Once());
            mock.Verify(x => x.GetTranslation("wox_plugin_program_open_in_console"), Times.Once());
        }

        [Test]
        public void Win32AppsShouldSetNameAsTitleWhileCreatingResult()
        {
            var mock = new Mock<IPublicAPI>();
            mock.Setup(x => x.GetTranslation(It.IsAny<string>())).Returns(It.IsAny<string>());
            StringMatcher.Instance = new StringMatcher();

            // Act
            var result = cmder_run_command.Result("cmder", mock.Object);

            // Assert
            Assert.IsTrue(result.Title.Equals(cmder_run_command.Name));
            Assert.IsFalse(result.Title.Equals(cmder_run_command.Description));
        }
    }
}
