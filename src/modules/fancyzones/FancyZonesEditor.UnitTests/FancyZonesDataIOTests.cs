// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json;
using FancyZonesEditor.Models;
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
        LayoutModel[] editorTemplates =
        [
            new CanvasLayoutModel("blank", LayoutType.Blank) { TemplateZoneCount = 0, SensitivityRadius = 0 },
            new CanvasLayoutModel("focus", LayoutType.Focus),
            new GridLayoutModel("columns", LayoutType.Columns),
            new GridLayoutModel("rows", LayoutType.Rows),
            new GridLayoutModel("grid", LayoutType.Grid),
            new GridLayoutModel("priority-grid", LayoutType.PriorityGrid),
        ];

        Assert.AreEqual(editorTemplates.Length, templates.Count);
        for (int i = 0; i < editorTemplates.Length; i++)
        {
            var model = editorTemplates[i];
            Assert.AreEqual(model.Name, templates[i].Type);
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
        Assert.ThrowsExactly<FileNotFoundException>(() => FancyZonesDataIO.ReadLayoutTemplates(_directory));
        Assert.ThrowsExactly<FileNotFoundException>(() => FancyZonesDataIO.ReadCustomLayouts(_directory));
    }
}
