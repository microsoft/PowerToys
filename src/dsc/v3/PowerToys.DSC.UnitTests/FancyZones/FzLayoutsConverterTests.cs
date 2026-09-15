// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FancyZonesEditorCommon.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Models.FancyZones;

namespace PowerToys.DSC.UnitTests.FancyZones;

[TestClass]
public sealed class FzLayoutsConverterTests
{
    private const string GridGuid = "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}";
    private const string CanvasGuid = "{8F0B6D3E-2C41-4A5B-9D7E-0F1A2B3C4D5E}";

    private const string EditorCustomLayoutsJson = /*lang=json,strict*/ """
        {
          "custom-layouts": [
            {
              "uuid": "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}",
              "name": "Two columns",
              "type": "grid",
              "info": {
                "rows": 1,
                "columns": 2,
                "rows-percentage": [ 10000 ],
                "columns-percentage": [ 7000, 3000 ],
                "cell-child-map": [ [ 0, 1 ] ],
                "show-spacing": false,
                "spacing": 4,
                "sensitivity-radius": 30
              }
            },
            {
              "uuid": "{8F0B6D3E-2C41-4A5B-9D7E-0F1A2B3C4D5E}",
              "name": "Reference window",
              "type": "canvas",
              "info": {
                "ref-width": 1920,
                "ref-height": 1080,
                "zones": [
                  { "X": 0, "Y": 0, "width": 1280, "height": 1080 },
                  { "X": 1280, "Y": 0, "width": 640, "height": 1080 }
                ],
                "sensitivity-radius": 20
              }
            }
          ]
        }
        """;

    [TestMethod]
    public void FromCustomLayouts_ConvertsEditorShape()
    {
        // Arrange
        var stored = JsonSerializer.Deserialize(EditorCustomLayoutsJson, FancyZonesJsonContext.Default.CustomLayoutListWrapper);
        var warnings = new List<string>();

        // Act
        var layouts = FzLayoutsConverter.FromCustomLayouts(stored, warnings);

        // Assert
        Assert.AreEqual(0, warnings.Count);
        Assert.AreEqual(2, layouts.Count);

        var grid = layouts[0];
        Assert.AreEqual(GridGuid, grid.Uuid);
        Assert.AreEqual("Two columns", grid.Name);
        Assert.IsNull(grid.Canvas);
        Assert.IsNotNull(grid.Grid);
        Assert.AreEqual(1, grid.Grid.Rows);
        Assert.AreEqual(2, grid.Grid.Columns);
        CollectionAssert.AreEqual(new List<int> { 10000 }, grid.Grid.RowsPercentage);
        CollectionAssert.AreEqual(new List<int> { 7000, 3000 }, grid.Grid.ColumnsPercentage);
        Assert.AreEqual(1, grid.Grid.CellChildMap.Count);
        CollectionAssert.AreEqual(new List<int> { 0, 1 }, grid.Grid.CellChildMap[0]);
        Assert.IsFalse(grid.Grid.ShowSpacing);
        Assert.AreEqual(4, grid.Grid.Spacing);
        Assert.AreEqual(30, grid.Grid.SensitivityRadius);

        var canvas = layouts[1];
        Assert.AreEqual(CanvasGuid, canvas.Uuid);
        Assert.IsNull(canvas.Grid);
        Assert.IsNotNull(canvas.Canvas);
        Assert.AreEqual(1920, canvas.Canvas.RefWidth);
        Assert.AreEqual(1080, canvas.Canvas.RefHeight);
        Assert.AreEqual(2, canvas.Canvas.Zones.Count);
        Assert.AreEqual(1280, canvas.Canvas.Zones[1].X);
        Assert.AreEqual(640, canvas.Canvas.Zones[1].Width);
        Assert.AreEqual(20, canvas.Canvas.SensitivityRadius);
    }

    [TestMethod]
    public void ToCustomLayouts_ProducesEditorCompatibleShape()
    {
        // Arrange
        var model = CreateSampleModel();

        // Act
        var stored = FzLayoutsConverter.ToCustomLayouts(model.Custom);
        var json = JsonNode.Parse(new CustomLayouts().Serialize(stored));

        // Assert: kebab-case keys, upper-case X/Y and braced upper-case GUIDs
        var layouts = json["custom-layouts"].AsArray();
        Assert.AreEqual(2, layouts.Count);

        var grid = layouts[0];
        Assert.AreEqual(GridGuid, grid["uuid"].GetValue<string>());
        Assert.AreEqual("grid", grid["type"].GetValue<string>());
        Assert.AreEqual(2, grid["info"]["columns"].GetValue<int>());
        Assert.AreEqual(3000, grid["info"]["columns-percentage"][1].GetValue<int>());
        Assert.AreEqual(1, grid["info"]["cell-child-map"][0][1].GetValue<int>());
        Assert.IsTrue(grid["info"]["show-spacing"].GetValue<bool>());
        Assert.AreEqual(16, grid["info"]["spacing"].GetValue<int>());
        Assert.AreEqual(20, grid["info"]["sensitivity-radius"].GetValue<int>());

        var canvas = layouts[1];
        Assert.AreEqual(CanvasGuid, canvas["uuid"].GetValue<string>());
        Assert.AreEqual("canvas", canvas["type"].GetValue<string>());
        Assert.AreEqual(1920, canvas["info"]["ref-width"].GetValue<int>());
        Assert.AreEqual(1280, canvas["info"]["zones"][1]["X"].GetValue<int>());
        Assert.AreEqual(0, canvas["info"]["zones"][1]["Y"].GetValue<int>());
        Assert.AreEqual(640, canvas["info"]["zones"][1]["width"].GetValue<int>());
        Assert.AreEqual(20, canvas["info"]["sensitivity-radius"].GetValue<int>());
    }

    [TestMethod]
    public void ToDefaultLayouts_ProducesEditorCompatibleShape()
    {
        // Arrange
        var model = CreateSampleModel();

        // Act
        var stored = FzLayoutsConverter.ToDefaultLayouts(model.Defaults, model.Custom);
        var json = JsonNode.Parse(new DefaultLayouts().Serialize(stored));

        // Assert
        var defaults = json["default-layouts"].AsArray();
        Assert.AreEqual(2, defaults.Count);

        var horizontal = defaults[0];
        Assert.AreEqual("horizontal", horizontal["monitor-configuration"].GetValue<string>());
        Assert.AreEqual("custom", horizontal["layout"]["type"].GetValue<string>());
        Assert.AreEqual(GridGuid, horizontal["layout"]["uuid"].GetValue<string>());
        Assert.IsTrue(horizontal["layout"]["show-spacing"].GetValue<bool>());
        Assert.AreEqual(16, horizontal["layout"]["spacing"].GetValue<int>());
        Assert.AreEqual(0, horizontal["layout"]["zone-count"].GetValue<int>());
        Assert.AreEqual(0, horizontal["layout"]["sensitivity-radius"].GetValue<int>());

        var vertical = defaults[1];
        Assert.AreEqual("vertical", vertical["monitor-configuration"].GetValue<string>());
        Assert.AreEqual("rows", vertical["layout"]["type"].GetValue<string>());
        Assert.AreEqual(string.Empty, vertical["layout"]["uuid"].GetValue<string>());
        Assert.AreEqual(2, vertical["layout"]["zone-count"].GetValue<int>());
        Assert.AreEqual(20, vertical["layout"]["sensitivity-radius"].GetValue<int>());
    }

    [TestMethod]
    public void Canonicalize_IsIdempotent()
    {
        // Arrange
        var model = CreateSampleModel();

        // Act
        var first = FzLayoutsConverter.Canonicalize(model);
        var second = FzLayoutsConverter.Canonicalize(first);

        // Assert
        AssertLayoutsAreEqual(first, second);
    }

    [TestMethod]
    public void Canonicalize_FillsDefaults()
    {
        // Act
        var canonical = FzLayoutsConverter.Canonicalize(CreateSampleModel());

        // Assert: custom layouts
        Assert.IsTrue(canonical.Custom[0].Grid.ShowSpacing);
        Assert.AreEqual(16, canonical.Custom[0].Grid.Spacing);
        Assert.AreEqual(20, canonical.Custom[0].Grid.SensitivityRadius);
        Assert.AreEqual(20, canonical.Custom[1].Canvas.SensitivityRadius);

        // Templates: grid types get spacing defaults, focus gets none
        Assert.AreEqual("focus", canonical.Templates[0].Type);
        Assert.AreEqual(4, canonical.Templates[0].ZoneCount);
        Assert.IsFalse(canonical.Templates[0].ShowSpacing);
        Assert.AreEqual(0, canonical.Templates[0].Spacing);
        Assert.AreEqual(20, canonical.Templates[0].SensitivityRadius);
        Assert.AreEqual("priority-grid", canonical.Templates[1].Type);
        Assert.AreEqual(3, canonical.Templates[1].ZoneCount);
        Assert.IsTrue(canonical.Templates[1].ShowSpacing);
        Assert.AreEqual(16, canonical.Templates[1].Spacing);

        // Defaults: a template default has no uuid, a custom default copies the grid spacing
        var vertical = canonical.Defaults.Vertical;
        Assert.IsNull(vertical.Uuid);
        Assert.AreEqual(2, vertical.ZoneCount);
        Assert.IsTrue(vertical.ShowSpacing);
        Assert.AreEqual(16, vertical.Spacing);
        Assert.AreEqual(20, vertical.SensitivityRadius);

        var horizontal = canonical.Defaults.Horizontal;
        Assert.AreEqual(GridGuid, horizontal.Uuid);
        Assert.IsTrue(horizontal.ShowSpacing);
        Assert.AreEqual(16, horizontal.Spacing);
        Assert.AreEqual(0, horizontal.ZoneCount);
        Assert.AreEqual(0, horizontal.SensitivityRadius);
    }

    [TestMethod]
    public void Canonicalize_SortsHotkeysAndTemplates_PreservesCustomOrder()
    {
        // Arrange
        var model = new FzLayoutsModel
        {
            Custom = [CreateCanvasLayout(CanvasGuid), CreateGridLayout(GridGuid)],
            Templates = [new() { Type = "priority-grid" }, new() { Type = "focus" }, new() { Type = "rows" }],
            Hotkeys =
            [
                new() { Key = 3, LayoutId = GridGuid },
                new() { Key = 1, LayoutId = CanvasGuid },
            ],
        };

        // Act
        var canonical = FzLayoutsConverter.Canonicalize(model);

        // Assert
        Assert.AreEqual(CanvasGuid, canonical.Custom[0].Uuid);
        Assert.AreEqual(GridGuid, canonical.Custom[1].Uuid);
        CollectionAssert.AreEqual(new List<string> { "focus", "rows", "priority-grid" }, canonical.Templates.Select(t => t.Type).ToList());
        CollectionAssert.AreEqual(new List<int> { 1, 3 }, canonical.Hotkeys.Select(h => h.Key).ToList());
    }

    [TestMethod]
    public void Canonicalize_UnsetSectionsStayUnset()
    {
        // Arrange
        var model = new FzLayoutsModel { Hotkeys = [new() { Key = 1, LayoutId = GridGuid }] };

        // Act
        var canonical = FzLayoutsConverter.Canonicalize(model);

        // Assert
        Assert.IsNull(canonical.Custom);
        Assert.IsNull(canonical.Templates);
        Assert.IsNull(canonical.Defaults);
        Assert.AreEqual(1, canonical.Hotkeys.Count);
    }

    [TestMethod]
    public void Canonicalize_CustomDefault_ResolvesGridFromCurrentLayouts()
    {
        // Arrange: the model does not define custom layouts, the current state does
        var current = new List<FzCustomLayout> { CreateGridLayout(GridGuid) };
        current[0].Grid.ShowSpacing = false;
        current[0].Grid.Spacing = 8;
        var model = new FzLayoutsModel
        {
            Defaults = new() { Horizontal = new() { Type = "custom", Uuid = GridGuid } },
        };

        // Act
        var canonical = FzLayoutsConverter.Canonicalize(model, current);

        // Assert
        Assert.IsFalse(canonical.Defaults.Horizontal.ShowSpacing);
        Assert.AreEqual(8, canonical.Defaults.Horizontal.Spacing);
    }

    [DataTestMethod]
    [DataRow("5c4f1a20-9b3e-4c7d-8e2f-1a2b3c4d5e6f", true, GridGuid)]
    [DataRow("{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}", true, GridGuid)]
    [DataRow(" {5c4f1a20-9b3e-4c7d-8e2f-1a2b3c4d5e6f} ", true, GridGuid)]
    [DataRow("not-a-guid", false, null)]
    [DataRow("", false, null)]
    [DataRow(null, false, null)]
    public void TryNormalizeGuid_NormalizesToEditorForm(string input, bool expectedResult, string expectedGuid)
    {
        // Act
        var result = FzLayoutsConverter.TryNormalizeGuid(input, out var normalized);

        // Assert
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedGuid, normalized);
    }

    [TestMethod]
    public void Validate_ValidModel_NoErrors()
    {
        // Arrange
        var warnings = new List<string>();

        // Act
        var errors = FzLayoutsConverter.Validate(CreateSampleModel(), null, warnings);

        // Assert
        Assert.AreEqual(0, errors.Count, string.Join(" | ", errors));
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void Validate_EmptyModel_Warns()
    {
        // Arrange
        var warnings = new List<string>();

        // Act
        var errors = FzLayoutsConverter.Validate(new FzLayoutsModel(), null, warnings);

        // Assert
        Assert.AreEqual(0, errors.Count);
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "nothing is managed");
    }

    [TestMethod]
    public void Validate_LayoutReference_ErrorWhenCustomLayoutsDefined()
    {
        // Arrange
        var model = new FzLayoutsModel
        {
            Custom = [CreateGridLayout(GridGuid)],
            Hotkeys = [new() { Key = 1, LayoutId = CanvasGuid }],
        };

        // Act
        var errors = FzLayoutsConverter.Validate(model);

        // Assert
        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual($"hotkeys[0].layoutId: layout '{CanvasGuid}' is not defined in 'custom'", errors[0]);
    }

    [TestMethod]
    public void Validate_LayoutReference_WarnsAgainstCurrentState()
    {
        // Arrange
        var model = new FzLayoutsModel { Hotkeys = [new() { Key = 1, LayoutId = CanvasGuid }] };
        var current = new FzLayoutsModel { Custom = [CreateGridLayout(GridGuid)] };
        var warnings = new List<string>();

        // Act
        var errors = FzLayoutsConverter.Validate(model, current, warnings);

        // Assert
        Assert.AreEqual(0, errors.Count);
        Assert.AreEqual(1, warnings.Count);
        Assert.AreEqual($"hotkeys[0].layoutId: layout '{CanvasGuid}' does not exist in the current custom layouts", warnings[0]);
    }

    [TestMethod]
    public void Validate_LayoutReference_NoWarningWithoutCurrentState()
    {
        // Arrange
        var model = new FzLayoutsModel { Hotkeys = [new() { Key = 1, LayoutId = CanvasGuid }] };
        var warnings = new List<string>();

        // Act
        var errors = FzLayoutsConverter.Validate(model, null, warnings);

        // Assert
        Assert.AreEqual(0, errors.Count);
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void Validate_CanvasWithTooManyZones_Error()
    {
        // Arrange
        var layout = CreateCanvasLayout(CanvasGuid);
        layout.Canvas.Zones = Enumerable.Range(0, LayoutDefaultSettings.MaxZones + 1)
            .Select(i => new FzCanvasZone { X = i, Y = 0, Width = 10, Height = 10 })
            .ToList();
        var model = new FzLayoutsModel { Custom = [layout] };

        // Act
        var errors = FzLayoutsConverter.Validate(model);

        // Assert
        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual("custom[0].canvas.zones must not contain more than 128 zones", errors[0]);
    }

    [TestMethod]
    public void Validate_NegativeValues_Errors()
    {
        // Arrange
        var grid = CreateGridLayout(GridGuid);
        grid.Grid.Spacing = -1;
        grid.Grid.SensitivityRadius = -1;
        var model = new FzLayoutsModel
        {
            Custom = [grid],
            Templates = [new() { Type = "focus", ZoneCount = -1 }],
            Defaults = new() { Vertical = new() { Type = "blank", Spacing = -5 } },
        };

        // Act
        var errors = FzLayoutsConverter.Validate(model);

        // Assert
        CollectionAssert.AreEquivalent(
            new List<string>
            {
                "custom[0].grid.spacing must not be negative",
                "custom[0].grid.sensitivityRadius must not be negative",
                "templates[0].zoneCount must not be negative",
                "defaults.vertical.spacing must not be negative",
            },
            errors.ToList());
    }

    [TestMethod]
    public void FromCustomLayouts_SkipsEntriesTheEngineWouldNotLoad()
    {
        // Arrange
        var json = /*lang=json,strict*/ """
            {
              "custom-layouts": [
                { "uuid": "not-a-guid", "name": "Bad id", "type": "grid", "info": { "rows": 1, "columns": 1, "rows-percentage": [ 10000 ], "columns-percentage": [ 10000 ], "cell-child-map": [ [ 0 ] ] } },
                { "uuid": "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}", "name": "No info", "type": "grid" },
                { "uuid": "{8F0B6D3E-2C41-4A5B-9D7E-0F1A2B3C4D5E}", "name": "Unknown", "type": "hexagons", "info": {} }
              ]
            }
            """;
        var stored = JsonSerializer.Deserialize(json, FancyZonesJsonContext.Default.CustomLayoutListWrapper);
        var warnings = new List<string>();

        // Act
        var layouts = FzLayoutsConverter.FromCustomLayouts(stored, warnings);

        // Assert
        Assert.AreEqual(0, layouts.Count);
        Assert.AreEqual(3, warnings.Count);
        StringAssert.Contains(warnings[0], "invalid uuid 'not-a-guid'");
        StringAssert.Contains(warnings[1], "without layout info");
        StringAssert.Contains(warnings[2], "unknown type 'hexagons'");
    }

    [TestMethod]
    public void FromDefaultLayouts_UnknownMonitorConfiguration_TreatedAsHorizontal()
    {
        // Arrange: mirrors DefaultLayoutsJsonUtils::TypeFromString in the engine
        var json = /*lang=json,strict*/ """
            {
              "default-layouts": [
                { "monitor-configuration": "diagonal", "layout": { "uuid": "", "type": "columns", "show-spacing": true, "spacing": 16, "zone-count": 3, "sensitivity-radius": 20 } }
              ]
            }
            """;
        var stored = JsonSerializer.Deserialize(json, FancyZonesJsonContext.Default.DefaultLayoutsListWrapper);

        // Act
        var defaults = FzLayoutsConverter.FromDefaultLayouts(stored);

        // Assert
        Assert.IsNull(defaults.Vertical);
        Assert.IsNotNull(defaults.Horizontal);
        Assert.AreEqual("columns", defaults.Horizontal.Type);
        Assert.IsNull(defaults.Horizontal.Uuid);
    }

    private static FzLayoutsModel CreateSampleModel()
    {
        return new FzLayoutsModel
        {
            Custom = [CreateGridLayout(GridGuid), CreateCanvasLayout(CanvasGuid)],
            Templates =
            [
                new() { Type = "priority-grid", ZoneCount = 3 },
                new() { Type = "focus", ZoneCount = 4 },
            ],
            Hotkeys = [new() { Key = 1, LayoutId = GridGuid }],
            Defaults = new()
            {
                Horizontal = new() { Type = "custom", Uuid = GridGuid },
                Vertical = new() { Type = "rows", ZoneCount = 2 },
            },
        };
    }

    private static FzCustomLayout CreateGridLayout(string uuid)
    {
        return new FzCustomLayout
        {
            Uuid = uuid,
            Name = "Two columns",
            Grid = new()
            {
                Rows = 1,
                Columns = 2,
                RowsPercentage = [10000],
                ColumnsPercentage = [7000, 3000],
                CellChildMap = [[0, 1]],
            },
        };
    }

    private static FzCustomLayout CreateCanvasLayout(string uuid)
    {
        return new FzCustomLayout
        {
            Uuid = uuid,
            Name = "Reference window",
            Canvas = new()
            {
                RefWidth = 1920,
                RefHeight = 1080,
                Zones =
                [
                    new() { X = 0, Y = 0, Width = 1280, Height = 1080 },
                    new() { X = 1280, Y = 0, Width = 640, Height = 1080 },
                ],
            },
        };
    }

    private static void AssertLayoutsAreEqual(FzLayoutsModel expected, FzLayoutsModel actual)
    {
        var expectedJson = JsonSerializer.SerializeToNode(expected);
        var actualJson = JsonSerializer.SerializeToNode(actual);
        Assert.IsTrue(JsonNode.DeepEquals(expectedJson, actualJson), $"{expectedJson} != {actualJson}");
    }
}
