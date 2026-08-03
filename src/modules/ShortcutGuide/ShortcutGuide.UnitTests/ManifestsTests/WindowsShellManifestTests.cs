// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Models;
using YamlDotNet.Serialization;

namespace ShortcutGuide.UnitTests.ManifestsTests;

[TestClass]
public sealed class WindowsShellManifestTests
{
    [TestMethod]
    public void DesktopShortcuts_ContainsDesktopPeekShortcut()
    {
        var manifest = LoadWindowsShellManifest();
        var desktopShortcuts = manifest.Shortcuts.Single(category => category.SectionName == "Desktop Shortcuts");
        var desktopPeekShortcut = desktopShortcuts.Properties.Single(entry => entry.Name == "Peek at desktop temporarily");

        Assert.AreEqual(1, desktopPeekShortcut.Shortcut.Length);
        Assert.AreEqual(new ShortcutDescription(ctrl: false, shift: false, alt: false, win: true, keys: [","]), desktopPeekShortcut.Shortcut[0]);
    }

    private static ShortcutFile LoadWindowsShellManifest()
    {
        string manifestPath = Path.Combine(GetRepositoryRoot(), "src", "modules", "ShortcutGuide", "ShortcutGuide.Ui", "Assets", "ShortcutGuide", "Manifests", "+WindowsNT.Shell.en-US.yml");
        Deserializer deserializer = new();
        return deserializer.Deserialize<ShortcutFile>(File.ReadAllText(manifestPath));
    }

    private static string GetRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "..", "..", ".."));
    }
}
