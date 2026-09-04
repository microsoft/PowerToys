// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Helpers;
using ShortcutGuide.IndexYmlGenerator;
using ShortcutGuide.Models;

namespace ShortcutGuide.UnitTests.ManifestTests;

[TestClass]
public sealed class ManifestDiscoveryAndLoadingTests
{
    private string _tempDirectory = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ShortcutGuide_ManifestTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithMalformedManifest_SkipsBadFileAndIndexesValidManifest()
    {
        // 1. Arrange a valid manifest
        string validManifestContent = @"
PackageName: Test.ValidApp
Name: Valid App
WindowFilter: 'ValidApp.exe'
BackgroundProcess: false
Shortcuts:
  - SectionName: General
    Properties:
      - Name: Open
        Shortcut:
        - Win: false
          Ctrl: true
          Alt: false
          Shift: false
          Keys:
            - O
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Test.ValidApp.en-US.yml"), validManifestContent);

        // 2. Arrange a corrupted/malformed YAML manifest that previously aborted index generation with CLR exception
        string malformedManifestContent = "PackageName: [invalid: yaml: syntax:::";
        File.WriteAllText(Path.Combine(_tempDirectory, "Corrupt.Manifest.yml"), malformedManifestContent);

        // 3. Act: Generate index.yml
        IndexYmlGenerator.IndexYmlGenerator.CreateIndexYmlFile(_tempDirectory);

        // 4. Assert: index.yml was generated and contains the valid manifest
        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        Assert.IsTrue(File.Exists(indexPath), "index.yml should be generated even when malformed manifests exist.");

        string indexContent = File.ReadAllText(indexPath);
        StringAssert.Contains(indexContent, "Test.ValidApp");
    }

    [TestMethod]
    public void GetShortcutsOfApplication_UnlocalizedManifest_LoadsSuccessfully()
    {
        // Arrange: create an unlocalized manifest (+WindowsNT.Notepad-custom.yml)
        string manifestContent = @"
PackageName: +WindowsNT.Notepad-custom
Name: Custom Notepad
WindowFilter: 'Notepad.exe'
BackgroundProcess: false
Shortcuts:
  - SectionName: Edit
    Properties:
      - Name: Custom Shortcut
        Shortcut:
        - Win: false
          Ctrl: true
          Alt: true
          Shift: false
          Keys:
            - C
";
        File.WriteAllText(Path.Combine(_tempDirectory, "+WindowsNT.Notepad-custom.yml"), manifestContent);

        // Act: load using GetShortcutsOfApplication with unlocalized name
        ShortcutFile result = ManifestInterpreter.GetShortcutsOfApplication("+WindowsNT.Notepad-custom", _tempDirectory);

        // Assert: manifest fields deserialized correctly
        Assert.AreEqual("+WindowsNT.Notepad-custom", result.PackageName);
        Assert.AreEqual("Custom Notepad", result.Name);
        Assert.AreEqual("Notepad.exe", result.WindowFilter);
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithValidSymbolicLink_SuccessfullyIndexesTarget()
    {
        // Arrange: create a manifest outside the target folder
        string outsideDir = Path.Combine(Path.GetTempPath(), "ShortcutGuide_TargetDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);

        try
        {
            string targetFilePath = Path.Combine(outsideDir, "TargetApp.en-US.yml");
            string manifestContent = @"
PackageName: Target.App
Name: Target App
WindowFilter: 'TargetApp.exe'
BackgroundProcess: false
";
            File.WriteAllText(targetFilePath, manifestContent);

            string symlinkPath = Path.Combine(_tempDirectory, "SymlinkedApp.en-US.yml");

            try
            {
                File.CreateSymbolicLink(symlinkPath, targetFilePath);
            }
            catch (UnauthorizedAccessException)
            {
                // In non-elevated CI environments or environments without Developer Mode,
                // symlink creation is not permitted by Windows. Skip gracefully.
                Assert.Inconclusive("Skipping symlink test: privilege to create symbolic links is not held in this environment.");
                return;
            }

            // Act
            IndexYmlGenerator.IndexYmlGenerator.CreateIndexYmlFile(_tempDirectory);

            // Assert
            string indexPath = Path.Combine(_tempDirectory, "index.yml");
            Assert.IsTrue(File.Exists(indexPath), "index.yml should be generated.");
            string indexContent = File.ReadAllText(indexPath);
            StringAssert.Contains(indexContent, "Target.App");
        }
        finally
        {
            if (Directory.Exists(outsideDir))
            {
                try
                {
                    Directory.Delete(outsideDir, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
