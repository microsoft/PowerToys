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
        StringAssert.Contains(indexContent, "- WindowFilter: 'TestApp.exe'");
        StringAssert.Contains(indexContent, "- 'Test.App'");
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

        // Intentionally invalid YAML syntax that CleanScalarSpan rejects with
        // YamlFormatException because of the flow collection syntax in scalar position.
        File.WriteAllText(
            Path.Combine(_tempDirectory, "Corrupted.en-US.yml"),
            "PackageName: [invalid: yaml: syntax:::");

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        Assert.IsTrue(File.Exists(indexPath), "index.yml should still be generated despite malformed files.");

        string indexContent = File.ReadAllText(indexPath);
        StringAssert.Contains(indexContent, "- 'Valid.App'");
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

        // Empty files and files missing required PackageName or WindowFilter are rejected
        // by TryParseManifestHeader with a logged warning.
        File.WriteAllText(Path.Combine(_tempDirectory, "Empty.en-US.yml"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDirectory, "WhitespaceOnly.en-US.yml"), "   \n\t   ");
        File.WriteAllText(
            Path.Combine(_tempDirectory, "MissingFilter.en-US.yml"),
            "PackageName: NoFilter.App\nName: App");
        File.WriteAllText(
            Path.Combine(_tempDirectory, "MissingPackage.en-US.yml"),
            "WindowFilter: NoPackage.exe\nName: App");

        var result = ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        Assert.IsTrue(File.Exists(indexPath));

        string indexContent = File.ReadAllText(indexPath);
        StringAssert.Contains(indexContent, "- 'Valid.App'");
        Assert.IsFalse(indexContent.Contains("NoFilter.App", StringComparison.Ordinal));
        Assert.IsFalse(indexContent.Contains("NoPackage.exe", StringComparison.Ordinal));
        Assert.AreEqual(4, result.Warnings.Count, "All four invalid files should produce warnings.");
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
- WindowFilter: 'Stale.exe'
    Apps:
      - 'Stale.App'
";
        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        File.WriteAllText(indexPath, staleIndexContent);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string updatedIndexContent = File.ReadAllText(indexPath);
        StringAssert.Contains(updatedIndexContent, "- 'Valid.App'");
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

    [TestMethod]
    public void CreateIndexYmlFile_WithBackgroundProcessAfterShortcuts_ParsesBackgroundProcessCorrectly()
    {
        string manifestWithBackgroundProcessAtEnd = @"
PackageName: Background.App
Name: Background App
WindowFilter: BackgroundWindow.exe
Shortcuts:
  - SectionName: Main
    Properties:
      - Name: Action
        Shortcut:
          - Win: false
            Keys: A
BackgroundProcess: true
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Background.App.en-US.yml"), manifestWithBackgroundProcessAtEnd);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        string indexContent = File.ReadAllText(indexPath);

        StringAssert.Contains(indexContent, "BackgroundProcess: true");
        StringAssert.Contains(indexContent, "Background.App");
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithSpecialCharactersAndReservedWords_EscapesCorrectlyAndDeserializesInYamlDotNet()
    {
        // Manifests with values that match YAML reserved patterns exactly ('no', '007',
        // 'null', '1:30', '*') and embedded single quotes ('App's Editor') to test that
        // single-quoting prevents type coercion in YamlDotNet.
        string manifest1 = @"
PackageName: 007
Name: Numeric Package
WindowFilter: no
BackgroundProcess: false
Shortcuts:
  - SectionName: Main
    Properties:
      - Name: Action
        Shortcut:
          - Win: false
            Keys: A
";
        string manifest2 = @"
PackageName: Contoso.App's.Editor
Name: Contoso App's Editor
WindowFilter: null
BackgroundProcess: false
";
        string manifest3 = @"
PackageName: Sexagesimal.App
Name: Sexagesimal App
WindowFilter: 1:30
BackgroundProcess: false
";
        string manifest4 = @"
PackageName: Global.Wildcard.App
Name: Wildcard App
WindowFilter: *
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Numeric.en-US.yml"), manifest1);
        File.WriteAllText(Path.Combine(_tempDirectory, "Contoso.en-US.yml"), manifest2);
        File.WriteAllText(Path.Combine(_tempDirectory, "Sexagesimal.en-US.yml"), manifest3);
        File.WriteAllText(Path.Combine(_tempDirectory, "Wildcard.en-US.yml"), manifest4);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        Assert.IsTrue(File.Exists(indexPath), "index.yml should have been generated.");

        string indexContent = File.ReadAllText(indexPath);

        // Verify single-quoted format and doubled quote escaping
        StringAssert.Contains(indexContent, "- WindowFilter: 'no'");
        StringAssert.Contains(indexContent, "- WindowFilter: 'null'");
        StringAssert.Contains(indexContent, "- WindowFilter: '1:30'");
        StringAssert.Contains(indexContent, "- WindowFilter: '*'");
        StringAssert.Contains(indexContent, "- '007'");
        StringAssert.Contains(indexContent, "- 'Contoso.App''s.Editor'");

        // Verify that YamlDotNet (as used by ShortcutGuide.Ui) parses the emitted index.yml accurately
        var deserializer = new YamlDotNet.Serialization.Deserializer();
        var deserialized = deserializer.Deserialize<ShortcutGuide.Models.IndexFile>(indexContent);

        Assert.AreEqual(4, deserialized.Index.Length);

        // Check '1:30' (sexagesimal).
        var item130 = Array.Find(deserialized.Index, i => i.WindowFilter == "1:30");
        Assert.IsFalse(string.IsNullOrEmpty(item130.WindowFilter), "Sexagesimal '1:30' should deserialize as string.");
        Assert.AreEqual("Sexagesimal.App", item130.Apps[0]);

        // Check '*' (wildcard).
        var itemWildcard = Array.Find(deserialized.Index, i => i.WindowFilter == "*");
        Assert.IsFalse(string.IsNullOrEmpty(itemWildcard.WindowFilter), "Wildcard '*' should deserialize as string.");
        Assert.AreEqual("Global.Wildcard.App", itemWildcard.Apps[0]);

        // Check 'no' (boolean keyword in YAML 1.1).
        var itemNo = Array.Find(deserialized.Index, i => i.WindowFilter == "no");
        Assert.IsFalse(string.IsNullOrEmpty(itemNo.WindowFilter), "Reserved keyword 'no' should deserialize as string, not boolean.");
        Assert.AreEqual("007", itemNo.Apps[0], "Leading-zero numeric string '007' should deserialize as string, not integer.");

        // Check 'null'.
        var itemNull = Array.Find(deserialized.Index, i => i.WindowFilter == "null");
        Assert.IsFalse(string.IsNullOrEmpty(itemNull.WindowFilter), "Reserved keyword 'null' should deserialize as string, not null.");
        Assert.AreEqual("Contoso.App's.Editor", itemNull.Apps[0], "Escaped single quotes should deserialize back to a single apostrophe.");
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithLeadingDigitExecutableNames_DoesNotInterfereWithIndexing()
    {
        // Documents that executables starting with digits ('7z.exe', '1password.exe') are
        // safe and valid.
        string manifest1 = @"
PackageName: 7zip.7zip
Name: 7-Zip
WindowFilter: 7z.exe
BackgroundProcess: false
";
        string manifest2 = @"
PackageName: AgileBits.1Password
Name: 1Password
WindowFilter: 1password.exe
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "7zip.en-US.yml"), manifest1);
        File.WriteAllText(Path.Combine(_tempDirectory, "1Password.en-US.yml"), manifest2);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        string indexContent = File.ReadAllText(indexPath);

        StringAssert.Contains(indexContent, "- WindowFilter: '7z.exe'");
        StringAssert.Contains(indexContent, "- '7zip.7zip'");
        StringAssert.Contains(indexContent, "- WindowFilter: '1password.exe'");
        StringAssert.Contains(indexContent, "- 'AgileBits.1Password'");

        var deserializer = new YamlDotNet.Serialization.Deserializer();
        var deserialized = deserializer.Deserialize<ShortcutGuide.Models.IndexFile>(indexContent);

        Assert.AreEqual(2, deserialized.Index.Length);
        Assert.IsTrue(Array.Exists(deserialized.Index, i => i.WindowFilter == "7z.exe" && i.Apps[0] == "7zip.7zip"));
        Assert.IsTrue(Array.Exists(deserialized.Index, i => i.WindowFilter == "1password.exe" && i.Apps[0] == "AgileBits.1Password"));
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithQuotedInputsAndTrailingComments_StripsQuotesAndCommentsCorrectly()
    {
        // Manifest with double/single quotes on inputs and trailing inline comments.
        string manifest = @"
PackageName: ""Quoted.Double.App"" # trailing comment on package
Name: Quoted Double App
WindowFilter: 'Commented.exe' # trailing comment on filter
BackgroundProcess: false # trailing comment on bool
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Quoted.en-US.yml"), manifest);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        string indexContent = File.ReadAllText(indexPath);

        StringAssert.Contains(indexContent, "- WindowFilter: 'Commented.exe'");
        StringAssert.Contains(indexContent, "- 'Quoted.Double.App'");
        Assert.IsFalse(indexContent.Contains('#'), "Trailing comments should not leak into index.yml.");

        var deserializer = new YamlDotNet.Serialization.Deserializer();
        var deserialized = deserializer.Deserialize<ShortcutGuide.Models.IndexFile>(indexContent);

        Assert.AreEqual(1, deserialized.Index.Length);
        Assert.AreEqual("Commented.exe", deserialized.Index[0].WindowFilter);
        Assert.AreEqual("Quoted.Double.App", deserialized.Index[0].Apps[0]);
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithCaseInsensitivePropertyKeys_ParsesSuccessfully()
    {
        // Manifest using non-standard casing for YAML keys.
        string manifest = @"
packagename: Case.Insensitive.App
name: Case App
WINDOWFILTER: CaseApp.exe
backgroundprocess: true
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Case.en-US.yml"), manifest);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        string indexContent = File.ReadAllText(indexPath);

        StringAssert.Contains(indexContent, "- WindowFilter: 'CaseApp.exe'");
        StringAssert.Contains(indexContent, "BackgroundProcess: true");
        StringAssert.Contains(indexContent, "- 'Case.Insensitive.App'");
    }

    [TestMethod]
    public void CreateIndexYmlFile_WithUnsortedInputs_ProducesDeterministicSortedOutput()
    {
        // Manifests written in deliberately reversed alphabetical order.
        string zebraManifest = @"
PackageName: Zebra.App
Name: Zebra App
WindowFilter: Zebra.exe
BackgroundProcess: false
";
        string alphaManifestApp2 = @"
PackageName: Zed.App
Name: Zed App
WindowFilter: Alpha.exe
BackgroundProcess: false
";
        string alphaManifestApp1 = @"
PackageName: Alpha.App
Name: Alpha App
WindowFilter: Alpha.exe
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Zebra.en-US.yml"), zebraManifest);
        File.WriteAllText(Path.Combine(_tempDirectory, "AlphaZed.en-US.yml"), alphaManifestApp2);
        File.WriteAllText(Path.Combine(_tempDirectory, "AlphaAlpha.en-US.yml"), alphaManifestApp1);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        string indexContent = File.ReadAllText(indexPath);

        // Alpha.exe should come before Zebra.exe.
        int alphaFilterPos = indexContent.IndexOf("WindowFilter: 'Alpha.exe'", StringComparison.Ordinal);
        int zebraFilterPos = indexContent.IndexOf("WindowFilter: 'Zebra.exe'", StringComparison.Ordinal);
        Assert.IsTrue(alphaFilterPos >= 0 && zebraFilterPos >= 0, "Both filters should be in the index.");
        Assert.IsTrue(alphaFilterPos < zebraFilterPos, "WindowFilter 'Alpha.exe' must be sorted before 'Zebra.exe'.");

        // Under Alpha.exe, 'Alpha.App' must be sorted before 'Zed.App'.
        int alphaAppPos = indexContent.IndexOf("- 'Alpha.App'", StringComparison.Ordinal);
        int zedAppPos = indexContent.IndexOf("- 'Zed.App'", StringComparison.Ordinal);
        Assert.IsTrue(alphaAppPos >= 0 && zedAppPos >= 0, "Both apps should be in the index.");
        Assert.IsTrue(alphaAppPos < zedAppPos, "'Alpha.App' must be sorted before 'Zed.App'.");
    }

    [TestMethod]
    public void CreateIndexYmlFile_CleansUpTempIndexFile()
    {
        string manifestContent = @"
PackageName: Valid.App
Name: Valid App
WindowFilter: ValidApp.exe
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "Valid.App.en-US.yml"), manifestContent);

        ManifestIndexGenerator.CreateIndexYmlFile(_tempDirectory);

        string tempPath = Path.Combine(_tempDirectory, "index.yml.tmp");
        Assert.IsFalse(File.Exists(tempPath), "index.yml.tmp should have been moved/cleaned up.");

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        Assert.IsTrue(File.Exists(indexPath), "index.yml should exist.");
    }

    [TestMethod]
    public void NeedsIndexRegeneration_WhenIndexDoesNotExist_ReturnsTrue()
    {
        string manifestContent = @"
PackageName: App.One
Name: App One
WindowFilter: AppOne.exe
BackgroundProcess: false
";
        File.WriteAllText(Path.Combine(_tempDirectory, "App.One.en-US.yml"), manifestContent);

        bool needsRegen = ManifestIndexGenerator.NeedsIndexRegeneration(_tempDirectory);

        Assert.IsTrue(needsRegen, "NeedsIndexRegeneration should return true when index.yml is missing.");
    }

    [TestMethod]
    public void NeedsIndexRegeneration_WhenIndexIsNewerThanAllManifests_ReturnsFalse()
    {
        string manifestPath = Path.Combine(_tempDirectory, "App.One.en-US.yml");
        string manifestContent = @"
PackageName: App.One
Name: App One
WindowFilter: AppOne.exe
BackgroundProcess: false
";
        File.WriteAllText(manifestPath, manifestContent);
        File.SetLastWriteTimeUtc(manifestPath, DateTime.UtcNow.AddMinutes(-10));

        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        File.WriteAllText(indexPath, "DefaultShellName: +WindowsNT.Shell\nIndex:\n");
        File.SetLastWriteTimeUtc(indexPath, DateTime.UtcNow);

        bool needsRegen = ManifestIndexGenerator.NeedsIndexRegeneration(_tempDirectory);

        Assert.IsFalse(needsRegen, "NeedsIndexRegeneration should return false when index.yml is newer than all manifests.");
    }

    [TestMethod]
    public void NeedsIndexRegeneration_WhenUserAddsOrModifiesManifest_ReturnsTrue()
    {
        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        File.WriteAllText(indexPath, "DefaultShellName: +WindowsNT.Shell\nIndex:\n");
        File.SetLastWriteTimeUtc(indexPath, DateTime.UtcNow.AddMinutes(-5));

        // Simulate a user creating or editing a custom manifest in the folder.
        string userManifestPath = Path.Combine(_tempDirectory, "Custom.UserApp.en-US.yml");
        string userManifestContent = @"
PackageName: Custom.UserApp
Name: Custom User App
WindowFilter: CustomApp.exe
BackgroundProcess: false
";
        File.WriteAllText(userManifestPath, userManifestContent);
        File.SetLastWriteTimeUtc(userManifestPath, DateTime.UtcNow);

        bool needsRegen = ManifestIndexGenerator.NeedsIndexRegeneration(_tempDirectory);

        Assert.IsTrue(needsRegen, "NeedsIndexRegeneration should return true when a user-added or modified manifest is newer than index.yml.");
    }

    [TestMethod]
    public void NeedsIndexRegeneration_IgnoresIndexYmlAndTempFiles()
    {
        string indexPath = Path.Combine(_tempDirectory, "index.yml");
        File.WriteAllText(indexPath, "DefaultShellName: +WindowsNT.Shell\nIndex:\n");
        File.SetLastWriteTimeUtc(indexPath, DateTime.UtcNow.AddMinutes(-5));

        // A temp file or index.yml itself should not be considered a newer manifest.
        string tempPath = Path.Combine(_tempDirectory, "index.yml.tmp");
        File.WriteAllText(tempPath, "temp");
        File.SetLastWriteTimeUtc(tempPath, DateTime.UtcNow.AddMinutes(-1));

        bool needsRegen = ManifestIndexGenerator.NeedsIndexRegeneration(_tempDirectory);

        Assert.IsFalse(needsRegen, "NeedsIndexRegeneration should ignore index.yml and index.yml.tmp.");
    }
}
