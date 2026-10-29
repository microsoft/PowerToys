// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class ShortcutDiscoverySettingsTests
{
    [TestMethod]
    [DataRow(Environment.SpecialFolder.Desktop, false, false)]
    [DataRow(Environment.SpecialFolder.Desktop, false, true)]
    [DataRow(Environment.SpecialFolder.Desktop, true, false)]
    [DataRow(Environment.SpecialFolder.Desktop, true, true)]
    [DataRow(Environment.SpecialFolder.CommonDesktopDirectory, false, false)]
    [DataRow(Environment.SpecialFolder.CommonDesktopDirectory, false, true)]
    [DataRow(Environment.SpecialFolder.CommonDesktopDirectory, true, false)]
    [DataRow(Environment.SpecialFolder.CommonDesktopDirectory, true, true)]
    [DataRow(Environment.SpecialFolder.StartMenu, false, false)]
    [DataRow(Environment.SpecialFolder.StartMenu, false, true)]
    [DataRow(Environment.SpecialFolder.StartMenu, true, false)]
    [DataRow(Environment.SpecialFolder.StartMenu, true, true)]
    [DataRow(Environment.SpecialFolder.CommonStartMenu, false, false)]
    [DataRow(Environment.SpecialFolder.CommonStartMenu, false, true)]
    [DataRow(Environment.SpecialFolder.CommonStartMenu, true, false)]
    [DataRow(Environment.SpecialFolder.CommonStartMenu, true, true)]
    public void NonAppShortcut_UsesItsOwnDiscoverySetting(Environment.SpecialFolder folder, bool directoryTarget, bool include)
    {
        var isDesktop = folder is Environment.SpecialFolder.Desktop or Environment.SpecialFolder.CommonDesktopDirectory;
        var program = ReadShortcut(folder, directoryTarget, isDesktop ? include : !include, isDesktop ? !include : include);

        Assert.IsTrue(program.Valid);
        Assert.AreEqual(directoryTarget ? Win32Program.ApplicationType.Folder : Win32Program.ApplicationType.GenericFile, program.AppType);
        Assert.AreEqual(include, program.Enabled);
    }

    [TestMethod]
    [DataRow(Environment.SpecialFolder.Desktop, false, false)]
    [DataRow(Environment.SpecialFolder.Desktop, true, false)]
    [DataRow(Environment.SpecialFolder.StartMenu, false, true)]
    [DataRow(Environment.SpecialFolder.StartMenu, true, true)]
    public void NonAppShortcut_UsesDefaultSettings(Environment.SpecialFolder folder, bool directoryTarget, bool expectedEnabled)
    {
        var program = ReadShortcut(folder, directoryTarget);
        Assert.IsTrue(program.Valid);
        Assert.AreEqual(expectedEnabled, program.Enabled);
    }

    [TestMethod]
    [DataRow(Environment.SpecialFolder.Desktop)]
    [DataRow(Environment.SpecialFolder.CommonDesktopDirectory)]
    [DataRow(Environment.SpecialFolder.StartMenu)]
    [DataRow(Environment.SpecialFolder.CommonStartMenu)]
    public void ApplicationShortcut_RemainsEnabledWhenNonAppsAreExcluded(Environment.SpecialFolder folder)
    {
        var program = ReadShortcut(folder, directoryTarget: false, includeDesktop: false, includeStartMenu: false, applicationTarget: true);
        Assert.IsTrue(program.Valid);
        Assert.IsTrue(program.Enabled);
    }

    private static Win32Program ReadShortcut(
        Environment.SpecialFolder folder,
        bool directoryTarget,
        bool? includeDesktop = null,
        bool? includeStartMenu = null,
        bool applicationTarget = false)
    {
        var root = Path.Combine(Path.GetTempPath(), $"CmdPal-shortcut-settings-{Guid.NewGuid():N}");
        var shortcutDirectory = Path.Combine(root, folder.ToString());
        var targetDirectory = Path.Combine(root, "Target");
        var documentPath = Path.Combine(targetDirectory, "Document.txt");
        var shortcutPath = Path.Combine(shortcutDirectory, "Test.lnk");
        Directory.CreateDirectory(shortcutDirectory);
        Directory.CreateDirectory(targetDirectory);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            File.WriteAllText(documentPath, "Shortcut target");
            var settings = new AllAppsSettings(Path.Combine(root, "apps.settings.json"));
            if (includeDesktop is { } desktop && includeStartMenu is { } startMenu)
            {
                settings.Settings.Update(new JsonObject
                {
                    ["apps.IncludeNonAppsOnDesktop"] = desktop ? "true" : "false",
                    ["apps.IncludeNonAppsInStartMenu"] = startMenu ? "true" : "false",
                }.ToJsonString());
                Assert.AreEqual(desktop, settings.IncludeNonAppsOnDesktop);
                Assert.AreEqual(startMenu, settings.IncludeNonAppsInStartMenu);
            }

            shortcut.TargetPath = applicationTarget
                ? Path.Combine(Environment.SystemDirectory, "cmd.exe")
                : directoryTarget ? targetDirectory : documentPath;
            shortcut.Description = "Shortcut discovery settings test";
            shortcut.Save();

            return Win32Program.LnkProgram(shortcutPath, settings, knownFolder => Path.Combine(root, knownFolder.ToString()));
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            File.Delete(shortcutPath);
            File.Delete(documentPath);
            foreach (var file in Directory.EnumerateFiles(root))
            {
                File.Delete(file);
            }

            Directory.Delete(shortcutDirectory);
            Directory.Delete(targetDirectory);
            Directory.Delete(root);
        }
    }
}
