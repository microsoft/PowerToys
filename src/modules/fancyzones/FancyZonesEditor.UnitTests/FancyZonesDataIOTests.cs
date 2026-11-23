// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using FancyZonesEditor;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace UnitTestsFancyZonesEditor;

[TestClass]
public class FancyZonesDataIOTests
{
    private string _directory = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), nameof(FancyZonesDataIOTests), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void LayoutFilesCanBeMissingIndependently(bool hasTemplates, bool hasCustomLayouts)
    {
        const string templatesJson = """
            {"layout-templates":[{"type":"grid","show-spacing":false,"spacing":24,"zone-count":5,"sensitivity-radius":30}]}
            """;
        const string customLayoutsJson = """
            {"custom-layouts":[{"uuid":"{01234567-89AB-CDEF-0123-456789ABCDEF}","name":"Saved layout","type":"canvas","info":{"ref-width":1920,"ref-height":1080,"zones":[]}}]}
            """;
        string templatesPath = Path.Combine(_directory, "layout-templates.json");
        string customLayoutsPath = Path.Combine(_directory, "custom-layouts.json");
        if (hasTemplates)
        {
            File.WriteAllText(templatesPath, templatesJson);
        }

        if (hasCustomLayouts)
        {
            File.WriteAllText(customLayoutsPath, customLayoutsJson);
        }

        var templates = FancyZonesDataIO.ReadLayoutTemplates(templatesPath).LayoutTemplates;
        var customLayouts = FancyZonesDataIO.ReadCustomLayouts(customLayoutsPath).CustomLayouts;

        Assert.AreEqual(hasTemplates ? 1 : 6, templates.Count);
        Assert.AreEqual(hasCustomLayouts ? 1 : 0, customLayouts.Count);
        if (hasTemplates)
        {
            var expected = JsonSerializer.Deserialize(templatesJson, FancyZonesJsonContext.Default.TemplateLayoutsListWrapper);
            CollectionAssert.AreEqual(expected.LayoutTemplates, templates);
            Assert.AreEqual(templatesJson, File.ReadAllText(templatesPath));
        }

        if (hasCustomLayouts)
        {
            Assert.AreEqual("Saved layout", customLayouts[0].Name);
            Assert.AreEqual("{01234567-89AB-CDEF-0123-456789ABCDEF}", customLayouts[0].Uuid);
            Assert.AreEqual(1920, customLayouts[0].Info.GetProperty("ref-width").GetInt32());
            Assert.AreEqual(customLayoutsJson, File.ReadAllText(customLayoutsPath));
        }

        Assert.AreEqual(hasTemplates, File.Exists(templatesPath));
        Assert.AreEqual(hasCustomLayouts, File.Exists(customLayoutsPath));
    }

    [TestMethod]
    public void MissingTemplateFileUsesEditorDefaultsWithoutCreatingDirectory()
    {
        string missingDirectory = Path.Combine(_directory, "missing");
        var templates = FancyZonesDataIO.ReadLayoutTemplates(Path.Combine(missingDirectory, "layout-templates.json")).LayoutTemplates;
        var editorTemplates = MainWindowSettingsModel.CreateDefaultTemplateModels();

        Assert.AreEqual(editorTemplates.Length, templates.Count);
        for (int i = 0; i < editorTemplates.Length; i++)
        {
            var model = editorTemplates[i];
            Assert.AreEqual(FancyZonesEditorIO.LayoutTypeToJsonTag(model.Type), templates[i].Type);
            Assert.AreEqual(model.TemplateZoneCount, templates[i].ZoneCount);
            Assert.AreEqual(model.SensitivityRadius, templates[i].SensitivityRadius);
            Assert.AreEqual(model is GridLayoutModel grid && grid.ShowSpacing, templates[i].ShowSpacing);
            Assert.AreEqual(model is GridLayoutModel gridLayout ? gridLayout.Spacing : 0, templates[i].Spacing);
        }

        Assert.IsEmpty(FancyZonesDataIO.ReadCustomLayouts(Path.Combine(missingDirectory, "custom-layouts.json")).CustomLayouts);
        Assert.IsFalse(Directory.Exists(missingDirectory));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{invalid json")]
    public void InvalidLayoutFilesStillReportErrors(string contents)
    {
        string path = Path.Combine(_directory, "invalid.json");
        File.WriteAllText(path, contents);

        Assert.ThrowsExactly<JsonException>(() => FancyZonesDataIO.ReadLayoutTemplates(path));
        Assert.ThrowsExactly<JsonException>(() => FancyZonesDataIO.ReadCustomLayouts(path));
        Assert.AreEqual(contents, File.ReadAllText(path));
    }

    [TestMethod]
    public void ExistingEmptyCollectionsAreNotReplacedByDefaults()
    {
        string path = Path.Combine(_directory, "empty.json");
        File.WriteAllText(path, """{"layout-templates":[],"custom-layouts":[]}""");

        Assert.IsEmpty(FancyZonesDataIO.ReadLayoutTemplates(path).LayoutTemplates);
        Assert.IsEmpty(FancyZonesDataIO.ReadCustomLayouts(path).CustomLayouts);
    }

    [TestMethod]
    public void DirectoryAtLayoutFilePathStillReportsError()
    {
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => FancyZonesDataIO.ReadLayoutTemplates(_directory));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => FancyZonesDataIO.ReadCustomLayouts(_directory));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FileInParentPathStillReportsError(bool nested)
    {
        string parentFile = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(parentFile, "existing file");
        string path = nested ? Path.Combine(parentFile, "child", "layouts.json") : Path.Combine(parentFile, "layouts.json");

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => FancyZonesDataIO.ReadLayoutTemplates(path));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => FancyZonesDataIO.ReadCustomLayouts(path));
        Assert.AreEqual("existing file", File.ReadAllText(parentFile));
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 0)]
    [DataRow(false, 2)]
    [DataRow(true, 2)]
    public void OptionalReadersOpenNewFilesAndRetrySharingViolations(bool customLayouts, int sharingViolations)
    {
        const string contents = """
            {"layout-templates":[{"type":"grid","zone-count":7}],"custom-layouts":[{"name":"Created at open"}]}
            """;
        string path = Path.Combine(_directory, "created-at-open.json");
        var fileSystem = new OpeningFileSystem(path, contents, sharingViolations);
        var ioUtils = new IOUtils(fileSystem);

        if (customLayouts)
        {
            var layouts = FancyZonesDataIO.ReadCustomLayouts(path, ioUtils).CustomLayouts;
            Assert.HasCount(1, layouts);
            Assert.AreEqual("Created at open", layouts[0].Name);
        }
        else
        {
            var layouts = FancyZonesDataIO.ReadLayoutTemplates(path, ioUtils).LayoutTemplates;
            Assert.HasCount(1, layouts);
            Assert.AreEqual(7, layouts[0].ZoneCount);
        }

        Assert.AreEqual(0, fileSystem.ExistenceChecksBeforeOpen);
        Assert.AreEqual(sharingViolations + 1, fileSystem.OpenAttempts);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OptionalReadersReportSharingViolationsAfterRetryLimit(bool customLayouts)
    {
        string path = Path.Combine(_directory, "locked.json");
        var fileSystem = new OpeningFileSystem(path, "{}", sharingViolations: 10);
        var ioUtils = new IOUtils(fileSystem);

        Assert.ThrowsExactly<IOException>(() =>
        {
            if (customLayouts)
            {
                FancyZonesDataIO.ReadCustomLayouts(path, ioUtils);
            }
            else
            {
                FancyZonesDataIO.ReadLayoutTemplates(path, ioUtils);
            }
        });
        Assert.AreEqual(10, fileSystem.OpenAttempts);
    }

    private sealed class OpeningFileSystem : MockFileSystem
    {
        private readonly OpeningFile? _file;

        public OpeningFileSystem(string path, string contents, int sharingViolations)
        {
            _file = new OpeningFile(this, path, contents, sharingViolations);
        }

        public override IFile File => _file ?? base.File;

        public int OpenAttempts => _file!.OpenAttempts;

        public int ExistenceChecksBeforeOpen => _file!.ExistenceChecksBeforeOpen;
    }

    private sealed class OpeningFile : MockFile
    {
        private readonly MockFileSystem _fileSystem;
        private readonly string _path;
        private readonly string _contents;
        private readonly int _sharingViolations;

        public OpeningFile(MockFileSystem fileSystem, string path, string contents, int sharingViolations)
            : base(fileSystem)
        {
            _fileSystem = fileSystem;
            _path = path;
            _contents = contents;
            _sharingViolations = sharingViolations;
        }

        public int OpenAttempts { get; private set; }

        public int ExistenceChecksBeforeOpen { get; private set; }

        public override bool Exists(string? path)
        {
            if (OpenAttempts == 0)
            {
                ExistenceChecksBeforeOpen++;
            }

            return base.Exists(path);
        }

        public override FileSystemStream Open(string path, FileMode mode)
        {
            OpenAttempts++;
            _fileSystem.AddFile(_path, new MockFileData(_contents));
            if (OpenAttempts <= _sharingViolations)
            {
                throw new IOException("The file is temporarily locked.");
            }

            return base.Open(path, mode);
        }
    }
}
