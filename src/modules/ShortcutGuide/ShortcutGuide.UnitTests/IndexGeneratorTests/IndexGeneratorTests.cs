// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.IndexYmlGenerator;

namespace ShortcutGuide.UnitTests.IndexGeneratorTests;

[TestClass]
public sealed class IndexGeneratorTests
{
    private string _tempDirectory = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "ShortcutGuide_IndexTests_" + Guid.NewGuid().ToString("N"));
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithValidManifest_CreatesIndexFileWithExpectedContent()
    {
        string manifestContent = @"
PackageName: Test.App
Name: Test App
WindowFilter: TestApp.exe
BackgroundProcess: false
Shortcuts:
  - SectionName: General
    Properties:
      - Name: Test Shortcut
        Shortcut:
        - Win: false
          Ctrl: true
          Alt: false
          Shift: false
          Keys:
            - T
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Test.App.en-US.yml"), manifestContent);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        Assert.IsTrue(File.Exists(indexPath), "index.yml should have been generated.");

        string indexContent = File.ReadAllText(indexPath);
        StringAssert.Contains(indexContent, "Test.App");
        StringAssert.Contains(indexContent, "TestApp.exe");
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithMalformedYaml_SkipsBadFileAndIndexesValidManifest()
    {
        string validManifest = @"
PackageName: Valid.App
Name: Valid App
WindowFilter: ValidApp.exe
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Valid.App.en-US.yml"), validManifest);

        // Intentionally invalid YAML syntax that triggers YamlException during
        // deserialization.
        File.WriteAllText(
            Path.Combine(_tempDirectory, "Corrupted.en-US.yml"),
            "PackageName: [invalid: yaml: syntax:::");

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        Assert.IsTrue(File.Exists(indexPath), "index.yml should still be generated despite malformed files.");

        string indexContent = File.ReadAllText(indexPath);
        StringAssert.Contains(indexContent, "Valid.App");
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithEmptyOrMissingRequiredFields_SkipsInvalidFiles()
    {
        string validManifest = @"
PackageName: Valid.App
Name: Valid App
WindowFilter: ValidApp.exe
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Valid.App.en-US.yml"), validManifest);

        // Empty file and files missing required PackageName or WindowFilter deserialize
        // to default/empty values on the struct.
        File.WriteAllText(Path.Combine(_tempDirectory, "Empty.en-US.yml"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDirectory, "WhitespaceOnly.en-US.yml"), "   \n\t   ");
        File.WriteAllText(
            Path.Combine(_tempDirectory, "MissingFilter.en-US.yml"),
            "PackageName: NoFilter.App\nName: App");
        File.WriteAllText(
            Path.Combine(_tempDirectory, "MissingPackage.en-US.yml"),
            "WindowFilter: NoPackage.exe\nName: App");

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        Assert.IsTrue(File.Exists(indexPath));

        string indexContent = File.ReadAllText(indexPath);
        StringAssert.Contains(indexContent, "Valid.App");
        Assert.IsFalse(indexContent.Contains("NoFilter.App", StringComparison.Ordinal));
        Assert.IsFalse(indexContent.Contains("NoPackage.exe", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithExistingIndexFile_SkipsIndexDuringEnumerationAndOverwrites()
    {
        string validManifest = @"
PackageName: Valid.App
Name: Valid App
WindowFilter: ValidApp.exe
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Valid.App.en-US.yml"), validManifest);

        // Pre-create index.yml with obsolete data to verify it is ignored during reading
        // and is replaced.
        string staleIndexContent = @"
Index:
- WindowFilter: Stale.exe
    Apps:
      - Stale.App
";
        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        File.WriteAllText(indexPath, staleIndexContent);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string updatedIndexContent = File.ReadAllText(indexPath);
        StringAssert.Contains(updatedIndexContent, "Valid.App");
        Assert.IsFalse(updatedIndexContent.Contains("Stale.App", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithMultipleAppsSharingFilter_GroupsUnderSameFilterWithoutDuplicates()
    {
        string manifestApp1 = @"
PackageName: Shared.App1
Name: App 1
WindowFilter: SharedWindow.exe
BackgroundProcess: false
";
        string manifestApp2 = @"
PackageName: Shared.App2
Name: App 2
WindowFilter: SharedWindow.exe
BackgroundProcess: false
";

        // Duplicate manifest for App1 (e.g. localized variant or duplicate file).
        string manifestApp1Duplicate = @"
PackageName: Shared.App1
Name: App 1 Other Locale
WindowFilter: SharedWindow.exe
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Shared.App1.en-US.yml"), manifestApp1);
        File.WriteAllText(Path.Combine(_tempDirectory, "Shared.App2.en-US.yml"), manifestApp2);
        File.WriteAllText(Path.Combine(_tempDirectory, "Shared.App1.de-DE.yml"), manifestApp1Duplicate);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        string indexContent = File.ReadAllText(indexPath);

        StringAssert.Contains(indexContent, "Shared.App1");
        StringAssert.Contains(indexContent, "Shared.App2");

        // Verify Shared.App1 is not duplicated in the output.
        int firstIndex = indexContent.IndexOf("Shared.App1", StringComparison.Ordinal);
        int lastIndex = indexContent.LastIndexOf("Shared.App1", StringComparison.Ordinal);
        Assert.AreEqual(firstIndex, lastIndex, "Shared.App1 should appear only once in index.yml.");
    }
}
