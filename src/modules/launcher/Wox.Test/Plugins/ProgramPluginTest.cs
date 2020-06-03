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
    }
}
