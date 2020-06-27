using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Wox.Infrastructure;
using Wox.Plugin;
using Microsoft.Plugin.Program.Programs;
using Moq;
using System.IO;

namespace Wox.Test.Plugins
{
    [TestFixture]
    public class ProgramPluginTest
    {
        Win32 notepad_appdata = new Win32
        {
            Name = "Notepad",
            ExecutableName = "notepad.exe",
            FullPath = "c:\\windows\\system32\\notepad.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\accessories\\notepad.lnk"
        };

        Win32 notepad_users = new Win32
        {
            Name = "Notepad",
            ExecutableName = "notepad.exe",
            FullPath = "c:\\windows\\system32\\notepad.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\accessories\\notepad.lnk"
        };

        Win32 azure_command_prompt = new Win32
        {
            Name = "Microsoft Azure Command Prompt - v2.9",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\microsoft azure\\microsoft azure sdk for .net\\v2.9\\microsoft azure command prompt - v2.9.lnk"
        };

        Win32 visual_studio_command_prompt = new Win32
        {
            Name = "x64 Native Tools Command Prompt for VS 2019",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\visual studio 2019\\visual studio tools\\vc\\x64 native tools command prompt for vs 2019.lnk"
        };

        Win32 command_prompt = new Win32
        {
            Name = "Command Prompt",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath ="c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\system tools\\command prompt.lnk"
        };

        Win32 file_explorer = new Win32
        {
            Name = "File Explorer",
            ExecutableName = "File Explorer.lnk",
            FullPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\system tools\\file explorer.lnk",
            LnkResolvedPath = null
        };

        Win32 wordpad = new Win32
        {
            Name = "Wordpad",
            ExecutableName = "wordpad.exe",
            FullPath = "c:\\program files\\windows nt\\accessories\\wordpad.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\accessories\\wordpad.lnk"
        };

        Win32 wordpad_duplicate = new Win32
        {
            Name = "WORDPAD",
            ExecutableName = "WORDPAD.EXE",
            FullPath = "c:\\program files\\windows nt\\accessories\\wordpad.exe",
            LnkResolvedPath = null
        };

        Win32 twitter_pwa = new Win32
        {
            Name = "Twitter",
            FullPath = "c:\\program files (x86)\\google\\chrome\\application\\chrome_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\chrome apps\\twitter.lnk",
            Arguments = " --profile-directory=Default --app-id=jgeosdfsdsgmkedfgdfgdfgbkmhcgcflmi"
        };

        Win32 pinned_webpage = new Win32
        {
            Name = "Web page",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\web page.lnk",
            Arguments = "--profile-directory=Default --app-id=homljgmgpmcbpjbnjpfijnhipfkiclkd"
        };

        Win32 edge_named_pinned_webpage = new Win32
        {
            Name = "edge - Bing",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge_proxy.exe",
            LnkResolvedPath = "c:\\users\\powertoys\\appdata\\roaming\\microsoft\\windows\\start menu\\programs\\edge - bing.lnk",
            Arguments = "  --profile-directory=Default --app-id=aocfnapldcnfbofgmbbllojgocaelgdd"
        };

        Win32 msedge = new Win32
        {
            Name = "Microsoft Edge",
            ExecutableName = "msedge.exe",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\msedge.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\microsoft edge.lnk"
        };

        Win32 chrome = new Win32
        {
            Name = "Google Chrome",
            ExecutableName = "chrome.exe",
            FullPath = "c:\\program files (x86)\\google\\chrome\\application\\chrome.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\google chrome.lnk"
        };

        Win32 dummy_proxy_app = new Win32
        {
            Name = "Proxy App",
            ExecutableName = "test_proxy.exe",
            FullPath = "c:\\program files (x86)\\microsoft\\edge\\application\\test_proxy.exe",
            LnkResolvedPath = "c:\\programdata\\microsoft\\windows\\start menu\\programs\\test proxy.lnk"
        };

        Win32 cmd_run_command = new Win32
        {
            Name = "cmd",
            ExecutableName = "cmd.exe",
            FullPath = "c:\\windows\\system32\\cmd.exe",
            LnkResolvedPath = null,
            AppType = 3 // Run command
        };

        Win32 cmder_run_command = new Win32
        {
            Name = "Cmder",
            ExecutableName = "Cmder.exe",
            FullPath = "c:\\tools\\cmder\\cmder.exe",
            LnkResolvedPath = null,
            AppType = 3 // Run command
        };

        Win32 dummy_internetShortcut_app = new Win32
        {
            Name = "Shop Titans",
            ExecutableName = "Shop Titans.url",           
            FullPath = "steam://rungameid/1258080",
            ParentDirectory = "C:\\Users\\temp\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Steam",
            LnkResolvedPath = null
        };

        Win32 dummy_internetShortcut_app_duplicate = new Win32
        {
            Name = "Shop Titans",
            ExecutableName = "Shop Titans.url",
            FullPath = "steam://rungameid/1258080",
            ParentDirectory = "C:\\Users\\temp\\Desktop",
            LnkResolvedPath = null
        };

        [Test]
        public void DedupFunction_whenCalled_mustRemoveDuplicateNotepads()
        {
            // Arrange
            List<Win32> prgms = new List<Win32>();
            prgms.Add(notepad_appdata);
            prgms.Add(notepad_users);

            // Act
            Win32[] apps = Win32.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 1);
        }

        [Test]
        public void DedupFunction_whenCalled_MustRemoveInternetShortcuts()
        {
            // Arrange
            List<Win32> prgms = new List<Win32>();
            prgms.Add(dummy_internetShortcut_app);
            prgms.Add(dummy_internetShortcut_app_duplicate);

            // Act
            Win32[] apps = Win32.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 1);
        }

        [Test] 
        public void DedupFunction_whenCalled_mustNotRemovelnkWhichdoesNotHaveExe()
        {
            // Arrange
            List<Win32> prgms = new List<Win32>();
            prgms.Add(file_explorer);

            // Act
            Win32[] apps = Win32.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 1);
        }

        [Test]
        public void DedupFunction_mustRemoveDuplicates_forExeExtensionsWithoutLnkResolvedPath()
        {
            // Arrange
            List<Win32> prgms = new List<Win32>();
            prgms.Add(wordpad);
            prgms.Add(wordpad_duplicate);

            // Act
            Win32[] apps = Win32.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 1);
            Assert.IsTrue(!string.IsNullOrEmpty(apps[0].LnkResolvedPath));
        }

        [Test]
        public void DedupFunction_mustNotRemovePrograms_withSameExeNameAndFullPath()
        {
            // Arrange
            List<Win32> prgms = new List<Win32>();
            prgms.Add(azure_command_prompt);
            prgms.Add(visual_studio_command_prompt);
            prgms.Add(command_prompt);

            // Act
            Win32[] apps = Win32.DeduplicatePrograms(prgms.AsParallel());

            // Assert
            Assert.AreEqual(apps.Length, 3);
        }

        [Test]
        public void FunctionIsWebApplication_ShouldReturnTrue_ForWebApplications()
        {
            // The IsWebApplication(() function must return true for all PWAs and pinned web pages
            Assert.IsTrue(twitter_pwa.IsWebApplication());
            Assert.IsTrue(pinned_webpage.IsWebApplication());
            Assert.IsTrue(edge_named_pinned_webpage.IsWebApplication());

            // Should not filter apps whose executable name ends with proxy.exe
            Assert.IsFalse(dummy_proxy_app.IsWebApplication());
        }

        [TestCase("ignore")]
        public void FunctionFilterWebApplication_ShouldReturnFalse_WhenSearchingForTheMainApp(string query)
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
        public bool EdgeWebSites_ShouldBeFiltered_WhenSearchingForEdge(string query)
        {
            return pinned_webpage.FilterWebApplication(query);
        }

        [TestCase("chrome", ExpectedResult = true)]
        [TestCase("CHROME", ExpectedResult = true)]
        [TestCase("Google", ExpectedResult = true)]
        [TestCase("Google Chrome", ExpectedResult = true)]
        [TestCase("Google Chrome twitter", ExpectedResult = false)]
        public bool ChromeWebSites_ShouldBeFiltered_WhenSearchingForChrome(string query)
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
        public bool PinnedWebPages_ShouldNotBeFiltered_WhenSearchingForThem(string query, int Case)
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
            else if(Case == CASE_WEB_PAGE)
            {
                return pinned_webpage.FilterWebApplication(query);
            }
            else if(Case == CASE_EDGE_NAMED_WEBPAGE)
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
        public void Win32Applications_ShouldNotBeFiltered_WhenFilteringRunCommands(string query)
        {
            // Even if there is an exact match in the name or exe name, win32 applications should never be filtered
            Assert.IsTrue(command_prompt.QueryEqualsNameForRunCommands(query));
        }

        [TestCase("cmd")]
        [TestCase("Cmd")]
        [TestCase("CMD")]
        public void RunCommands_ShouldNotBeFiltered_OnExactMatch(string query)
        {
            // Partial matches should be filtered as cmd is not equal to cmder
            Assert.IsFalse(cmder_run_command.QueryEqualsNameForRunCommands(query));

            // the query matches the name (cmd) and is therefore not filtered (case-insensitive)
            Assert.IsTrue(cmd_run_command.QueryEqualsNameForRunCommands(query));
        }
    }
}
