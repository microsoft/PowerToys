// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Run.Plugin.WindowsTerminal;
using Microsoft.PowerToys.Run.Plugin.WindowsTerminal.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Plugin.WindowsTerminal.UnitTests
{
    [TestClass]
    public class TerminalHelperTests
    {
        [DataTestMethod]
        [DataRow("Windows PowerShell", true, true, "--window _quake --profile \"Windows PowerShell\"")]
        [DataRow("Windows PowerShell", false, true, "--window _quake --profile \"Windows PowerShell\"")]
        [DataRow("Windows PowerShell", true, false, "--window 0 nt --profile \"Windows PowerShell\"")]
        [DataRow("Windows PowerShell", false, false, " --profile \"Windows PowerShell\"")]
        public void ArgumentsTest(string profile, bool openNewTab, bool openQuake, string expectedArguments)
        {
            var arguments = TerminalHelper.GetArguments(profile, openNewTab, openQuake);
            Assert.AreEqual(arguments, expectedArguments);
        }

        [DataTestMethod]
        [DataRow("settings 1.11.2421.0.json")]
        [DataRow("settings 1.11.2421.0_2.json")]
        public void ParseSettingsTest(string file)
        {
            var terminal = new TerminalPackage(string.Empty, new Version(), string.Empty, string.Empty, string.Empty);

            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
            var settings = File.ReadAllText(settingsPath);
            var profiles = TerminalHelper.ParseSettings(terminal, settings);

            Assert.AreEqual(profiles.Count, 4);
        }

        [DataTestMethod]
        [DataRow(
            @"{""guid"":""{61c54bbd-c2c6-5271-96e7-009a87ff44bf}"",""name"":""Windows PowerShell"",""commandline"":""powershell.exe"",""hidden"":true}",
            "61c54bbd-c2c6-5271-96e7-009a87ff44bf",
            "Windows PowerShell",
            true)]
        [DataRow(
            @"{""name"":""Windows PowerShell"",""commandline"":""powershell.exe"",""hidden"":false}",
            null,
            "Windows PowerShell",
            false)]
        public void ParseProfilesTest(string json, string identifier, string name, bool hidden)
        {
            var profileElement = JsonDocument.Parse(json).RootElement;
            var terminal = new TerminalPackage(string.Empty, new Version(), string.Empty, string.Empty, string.Empty);
            var profile = TerminalHelper.ParseProfile(terminal, profileElement);

            var expectedIdentifier = identifier != null ? new Guid(identifier) : null as Guid?;
            Assert.AreEqual(profile.Terminal, terminal);
            Assert.AreEqual(profile.Identifier, expectedIdentifier);
            Assert.AreEqual(profile.Name, name);
            Assert.AreEqual(profile.Hidden, hidden);
        }
    }
}
