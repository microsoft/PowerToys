// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class ExtensionTemplateServiceTests
{
    private string _templateRoot = null!;
    private string _outputRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"{nameof(ExtensionTemplateServiceTests)}_{Guid.NewGuid():N}");
        _templateRoot = Path.Combine(tempRoot, "template");
        _outputRoot = Path.Combine(tempRoot, "output");

        Directory.CreateDirectory(_templateRoot);
        Directory.CreateDirectory(_outputRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        var tempRoot = Directory.GetParent(_templateRoot)?.FullName;
        if (!string.IsNullOrEmpty(tempRoot) && Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void CreateExtension_BuildsExtensionFromTemplateArchive()
    {
        // Arrange
        var archiveRoot = Path.Combine(_templateRoot, "archive");
        var templateProjectRoot = Path.Combine(archiveRoot, "TemplateCmdPalExtension", "TemplateCmdPalExtension");
        Directory.CreateDirectory(Path.Combine(templateProjectRoot, "Assets"));

        File.WriteAllText(
            Path.Combine(templateProjectRoot, "Program.cs"),
            "TemplateCmdPalExtension TemplateDisplayName FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");
        File.WriteAllBytes(Path.Combine(templateProjectRoot, "Assets", "Logo.png"), [0x89, 0x50, 0x4E, 0x47]);

        var templateZipPath = Path.Combine(_templateRoot, "template.zip");
        ZipFile.CreateFromDirectory(archiveRoot, templateZipPath);

        var service = new ExtensionTemplateService(templateZipPath);

        // Act
        service.CreateExtension("MyExtension", "My Display Name", _outputRoot);

        // Assert
        var programFile = Path.Combine(_outputRoot, "MyExtension", "MyExtension", "Program.cs");
        var imageFile = Path.Combine(_outputRoot, "MyExtension", "MyExtension", "Assets", "Logo.png");

        Assert.IsTrue(File.Exists(programFile));
        Assert.IsTrue(File.Exists(imageFile));
        StringAssert.Contains(File.ReadAllText(programFile), "MyExtension");
        StringAssert.Contains(File.ReadAllText(programFile), "My Display Name");
        Assert.IsFalse(File.ReadAllText(programFile).Contains("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", StringComparison.Ordinal));
        CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(imageFile));
    }

    [TestMethod]
    public void CopyTemplateFile_RewritesTextFiles()
    {
        // Arrange
        var sourceFile = Path.Combine(_templateRoot, "TemplateCmdPalExtension", "Program.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, "TemplateCmdPalExtension TemplateDisplayName FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");

        // Act
        ExtensionTemplateService.CopyTemplateFile(_templateRoot, sourceFile, _outputRoot, "MyExtension", "My Display Name", "11111111-1111-1111-1111-111111111111");

        // Assert
        var outputFile = Path.Combine(_outputRoot, "MyExtension", "Program.cs");
        Assert.IsTrue(File.Exists(outputFile));
        Assert.AreEqual(
            "MyExtension My Display Name 11111111-1111-1111-1111-111111111111",
            File.ReadAllText(outputFile));
    }

    [TestMethod]
    public void CopyTemplateFile_CopiesUnchangedTextFilesVerbatim()
    {
        // Arrange
        var sourceFile = Path.Combine(_templateRoot, "TemplateCmdPalExtension", "Properties", "launchSettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);

        var sourceBytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("{\"profiles\":{\"CmdPal\":{}}}"))
            .ToArray();
        File.WriteAllBytes(sourceFile, sourceBytes);

        // Act
        ExtensionTemplateService.CopyTemplateFile(_templateRoot, sourceFile, _outputRoot, "MyExtension", "My Display Name", "11111111-1111-1111-1111-111111111111");

        // Assert
        var outputFile = Path.Combine(_outputRoot, "MyExtension", "Properties", "launchSettings.json");
        Assert.IsTrue(File.Exists(outputFile));
        CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(outputFile));
    }

    [TestMethod]
    public void CopyTemplateFile_CopiesBinaryFilesWithoutRewritingContents()
    {
        // Arrange
        var sourceFile = Path.Combine(_templateRoot, "TemplateCmdPalExtension", "Assets", "Logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);

        var binaryContent = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00,
        };
        var embeddedText = Encoding.UTF8.GetBytes("TemplateCmdPalExtension TemplateDisplayName");
        File.WriteAllBytes(sourceFile, [.. binaryContent, .. embeddedText]);

        // Act
        ExtensionTemplateService.CopyTemplateFile(_templateRoot, sourceFile, _outputRoot, "MyExtension", "My Display Name", "11111111-1111-1111-1111-111111111111");

        // Assert
        var outputFile = Path.Combine(_outputRoot, "MyExtension", "Assets", "Logo.png");
        Assert.IsTrue(File.Exists(outputFile));
        CollectionAssert.AreEqual(File.ReadAllBytes(sourceFile), File.ReadAllBytes(outputFile));
    }

    [TestMethod]
    public void TemplateFileHandling_ThrowsForUnknownExtension()
    {
        var ex = Assert.ThrowsException<InvalidOperationException>(() => ExtensionTemplateService.GetTemplateFileHandling("template.svg"));

        StringAssert.Contains(ex.Message, ".svg");
    }

    [TestMethod]
    public void TemplateExtensionCategories_AreDisjointAndCoverTemplateZip()
    {
        var replaceTokens = ExtensionTemplateService.ReplaceTokensTemplateExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copyAsIs = ExtensionTemplateService.CopyAsIsTemplateExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allAccountedFor = replaceTokens.Concat(copyAsIs).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var templateZipExtensions = GetTemplateZipExtensions();

        CollectionAssert.AreEqual(Array.Empty<string>(), replaceTokens.Intersect(copyAsIs, StringComparer.OrdinalIgnoreCase).ToArray());
        CollectionAssert.AreEquivalent(templateZipExtensions.OrderBy(x => x).ToArray(), allAccountedFor.OrderBy(x => x).ToArray());
    }

    [TestMethod]
    public void TemplateZipFiles_AllUseKnownHandling()
    {
        using var archive = ZipFile.OpenRead(TemplateZipPath);
        var replaceTokens = ExtensionTemplateService.ReplaceTokensTemplateExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copyAsIs = ExtensionTemplateService.CopyAsIsTemplateExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(Path.GetExtension(entry.FullName))))
        {
            var extension = Path.GetExtension(entry.FullName);
            var expectedHandling = replaceTokens.Contains(extension)
                ? ExtensionTemplateService.TemplateFileHandling.ReplaceTokens
                : ExtensionTemplateService.TemplateFileHandling.CopyAsIs;

            Assert.AreEqual(expectedHandling, ExtensionTemplateService.GetTemplateFileHandling(entry.FullName), entry.FullName);
            Assert.IsTrue(replaceTokens.Contains(extension) || copyAsIs.Contains(extension), entry.FullName);
        }
    }

    private static string TemplateZipPath => Path.Combine(AppContext.BaseDirectory, "Assets", "template.zip");

    private static HashSet<string> GetTemplateZipExtensions()
    {
        using var archive = ZipFile.OpenRead(TemplateZipPath);
        return archive.Entries
            .Where(entry => !string.IsNullOrEmpty(Path.GetExtension(entry.FullName)))
            .Select(entry => Path.GetExtension(entry.FullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
