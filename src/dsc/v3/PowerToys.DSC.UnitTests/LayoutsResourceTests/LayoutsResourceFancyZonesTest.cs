// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models;
using PowerToys.DSC.Models.FancyZones;
using PowerToys.DSC.Models.FunctionData;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.UnitTests.LayoutsResourceTests;

[TestClass]
public sealed class LayoutsResourceFancyZonesTest : BaseDscTest
{
    private const string GridGuid = "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}";
    private const string CanvasGuid = "{8F0B6D3E-2C41-4A5B-9D7E-0F1A2B3C4D5E}";

    // Placeholders replaced in the data-row inputs, which must be constants
    private const string GridGuidToken = "$GRID_GUID";
    private const string CanvasGuidToken = "$CANVAS_GUID";
    private const string ValidGridToken = "$VALID_GRID";
    private const string ValidGridJson = /*lang=json,strict*/ """{"rows":1,"columns":2,"rowsPercentage":[10000],"columnsPercentage":[7000,3000],"cellChildMap":[[0,1]]}""";

    // Files in the exact shape written by the FancyZones editor
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
                "show-spacing": true,
                "spacing": 16,
                "sensitivity-radius": 20
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

    private const string EditorLayoutTemplatesJson = /*lang=json,strict*/ """
        {
          "layout-templates": [
            { "type": "focus", "show-spacing": false, "spacing": 0, "zone-count": 4, "sensitivity-radius": 20 },
            { "type": "priority-grid", "show-spacing": true, "spacing": 16, "zone-count": 3, "sensitivity-radius": 20 }
          ]
        }
        """;

    private const string EditorLayoutHotkeysJson = /*lang=json,strict*/ """
        {
          "layout-hotkeys": [
            { "key": 1, "layout-id": "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}" }
          ]
        }
        """;

    private const string EditorDefaultLayoutsJson = /*lang=json,strict*/ """
        {
          "default-layouts": [
            {
              "monitor-configuration": "horizontal",
              "layout": { "uuid": "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}", "type": "custom", "show-spacing": true, "spacing": 16, "zone-count": 0, "sensitivity-radius": 0 }
            },
            {
              "monitor-configuration": "vertical",
              "layout": { "uuid": "", "type": "rows", "show-spacing": true, "spacing": 16, "zone-count": 2, "sensitivity-radius": 20 }
            }
          ]
        }
        """;

    private Func<string> _originalDataFolder;
    private string _dataFolder;

    private static string Module => nameof(ModuleType.FancyZones);

    [TestInitialize]
    public void TestInitialize()
    {
        // Redirect the layout files to a fresh temporary folder so the tests
        // never touch the user's FancyZones data. The folder is not created
        // up front: a missing folder is a valid initial state.
        _originalDataFolder = LayoutsFunctionData.DataFolder;
        _dataFolder = Path.Combine(Path.GetTempPath(), "PowerToys.DSC.Tests", Guid.NewGuid().ToString("N"));
        LayoutsFunctionData.DataFolder = () => _dataFolder;
    }

    [TestCleanup]
    public void TestCleanup()
    {
        LayoutsFunctionData.DataFolder = _originalDataFolder;
        if (Directory.Exists(_dataFolder))
        {
            Directory.Delete(_dataFolder, true);
        }
    }

    [TestMethod]
    public void Get_NoFiles_ReturnsEmptySections()
    {
        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module);
        var state = result.OutputState<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.Messages().Count);
        Assert.AreEqual(0, state.Layouts.Custom.Count);
        Assert.AreEqual(0, state.Layouts.Templates.Count);
        Assert.AreEqual(0, state.Layouts.Hotkeys.Count);
        Assert.IsNotNull(state.Layouts.Defaults);
        Assert.IsNull(state.Layouts.Defaults.Horizontal);
        Assert.IsNull(state.Layouts.Defaults.Vertical);
    }

    [TestMethod]
    public void Get_EditorShapedFiles_Success()
    {
        // Arrange
        WriteEditorFiles();

        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module);
        var state = result.OutputState<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.Messages().Count);
        AssertLayoutsAreEqual(FzLayoutsConverter.Canonicalize(CreateSampleModel()), state.Layouts);
    }

    [TestMethod]
    public void Export_Success()
    {
        // Arrange
        WriteEditorFiles();

        // Act
        var result = ExecuteDscCommand<ExportCommand>("--resource", LayoutsResource.ResourceName, "--module", Module);
        var state = result.OutputState<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertLayoutsAreEqual(FzLayoutsConverter.Canonicalize(CreateSampleModel()), state.Layouts);
    }

    [TestMethod]
    public void SetWithDiff_Success()
    {
        // Arrange
        var input = CreateInput(CreateSampleModel());

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<string> { LayoutsResourceObject.LayoutsJsonPropertyName }, diff);
        AssertLayoutsAreEqual(FzLayoutsConverter.Canonicalize(CreateSampleModel()), state.Layouts);

        // The stored files use the exact editor encoding
        var customLayouts = ReadFile(LayoutsFunctionData.CustomLayoutsFileName);
        StringAssert.Contains(customLayouts, "\"custom-layouts\"");
        StringAssert.Contains(customLayouts, "\"ref-width\"");
        StringAssert.Contains(customLayouts, "\"X\"");
        StringAssert.Contains(customLayouts, "\"cell-child-map\"");
        StringAssert.Contains(customLayouts, GridGuid);
        StringAssert.Contains(customLayouts, CanvasGuid);
        Assert.AreEqual(2, JsonNode.Parse(customLayouts)["custom-layouts"].AsArray().Count);

        var templates = ReadFile(LayoutsFunctionData.LayoutTemplatesFileName);
        StringAssert.Contains(templates, "\"layout-templates\"");
        StringAssert.Contains(templates, "\"zone-count\"");

        var hotkeys = ReadFile(LayoutsFunctionData.LayoutHotkeysFileName);
        StringAssert.Contains(hotkeys, "\"layout-hotkeys\"");
        StringAssert.Contains(hotkeys, "\"layout-id\"");
        StringAssert.Contains(hotkeys, GridGuid);

        var defaults = ReadFile(LayoutsFunctionData.DefaultLayoutsFileName);
        StringAssert.Contains(defaults, "\"monitor-configuration\"");
        StringAssert.Contains(defaults, "\"uuid\": \"\"");
        StringAssert.Contains(defaults, GridGuid);
    }

    [TestMethod]
    public void SetTwice_SecondSetHasNoDiff()
    {
        // Arrange
        var input = CreateInput(CreateSampleModel());

        // Act
        var firstResult = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var secondResult = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (_, firstDiff) = firstResult.OutputStateAndDiff<LayoutsResourceObject>();
        var (_, secondDiff) = secondResult.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(firstResult.Success);
        Assert.IsTrue(secondResult.Success);
        CollectionAssert.AreEqual(new List<string> { LayoutsResourceObject.LayoutsJsonPropertyName }, firstDiff);
        CollectionAssert.AreEqual(new List<string>(), secondDiff);
    }

    [TestMethod]
    public void Set_PartialInput_LeavesOtherFilesUntouched()
    {
        // Arrange: only the custom layouts exist on disk
        WriteEditorFile(LayoutsFunctionData.CustomLayoutsFileName, EditorCustomLayoutsJson);
        var input = CreateInput(new FzLayoutsModel
        {
            Hotkeys = [new() { Key = 2, LayoutId = CanvasGuid }],
        });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<string> { LayoutsResourceObject.LayoutsJsonPropertyName }, diff);
        Assert.AreEqual(EditorCustomLayoutsJson, ReadFile(LayoutsFunctionData.CustomLayoutsFileName));
        Assert.IsTrue(File.Exists(Path.Combine(_dataFolder, LayoutsFunctionData.LayoutHotkeysFileName)));
        Assert.IsFalse(File.Exists(Path.Combine(_dataFolder, LayoutsFunctionData.LayoutTemplatesFileName)));
        Assert.IsFalse(File.Exists(Path.Combine(_dataFolder, LayoutsFunctionData.DefaultLayoutsFileName)));

        // The reported state combines the applied hotkeys with the untouched sections
        Assert.AreEqual(2, state.Layouts.Custom.Count);
        Assert.AreEqual(1, state.Layouts.Hotkeys.Count);
        Assert.AreEqual(CanvasGuid, state.Layouts.Hotkeys[0].LayoutId);
        Assert.AreEqual(0, state.Layouts.Templates.Count);
    }

    [TestMethod]
    public void Set_CreatesMissingFolder()
    {
        // Arrange
        Assert.IsFalse(Directory.Exists(_dataFolder));
        var input = CreateInput(new FzLayoutsModel
        {
            Templates = [new() { Type = "columns", ZoneCount = 2 }],
        });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(File.Exists(Path.Combine(_dataFolder, LayoutsFunctionData.LayoutTemplatesFileName)));
    }

    [TestMethod]
    public void Set_EmptyArray_ClearsFile()
    {
        // Arrange
        WriteEditorFile(LayoutsFunctionData.CustomLayoutsFileName, EditorCustomLayoutsJson);
        var input = /*lang=json,strict*/ """{"layouts":{"custom":[]}}""";

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<string> { LayoutsResourceObject.LayoutsJsonPropertyName }, diff);
        Assert.AreEqual(0, state.Layouts.Custom.Count);
        Assert.AreEqual(0, JsonNode.Parse(ReadFile(LayoutsFunctionData.CustomLayoutsFileName))["custom-layouts"].AsArray().Count);
    }

    [TestMethod]
    public void Set_GuidWithoutBraces_IsCanonicalized()
    {
        // Arrange
        var lowerCaseGuid = GridGuid.Trim('{', '}').ToLowerInvariant();
        var input = CreateInput(new FzLayoutsModel
        {
            Custom = [CreateGridLayout(lowerCaseGuid)],
            Hotkeys = [new() { Key = 1, LayoutId = lowerCaseGuid }],
        });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, _) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(GridGuid, state.Layouts.Custom[0].Uuid);
        Assert.AreEqual(GridGuid, state.Layouts.Hotkeys[0].LayoutId);
        StringAssert.Contains(ReadFile(LayoutsFunctionData.CustomLayoutsFileName), GridGuid);
        StringAssert.Contains(ReadFile(LayoutsFunctionData.LayoutHotkeysFileName), GridGuid);
    }

    [TestMethod]
    public void Set_FocusTemplate_NormalizesSpacing()
    {
        // Arrange: spacing does not apply to the focus template
        var input = CreateInput(new FzLayoutsModel
        {
            Templates = [new() { Type = "focus", ZoneCount = 4, ShowSpacing = true, Spacing = 16 }],
        });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, _) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsFalse(state.Layouts.Templates[0].ShowSpacing);
        Assert.AreEqual(0, state.Layouts.Templates[0].Spacing);
        StringAssert.Contains(ReadFile(LayoutsFunctionData.LayoutTemplatesFileName), "\"show-spacing\": false");
    }

    [TestMethod]
    public void Set_CustomDefault_CopiesGridSpacing()
    {
        // Arrange: the editor stores the spacing of the referenced grid layout
        var grid = CreateGridLayout(GridGuid);
        grid.Grid.Spacing = 24;
        var input = CreateInput(new FzLayoutsModel
        {
            Custom = [grid],
            Defaults = new() { Horizontal = new() { Type = "custom", Uuid = GridGuid } },
        });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, _) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        var horizontal = state.Layouts.Defaults.Horizontal;
        Assert.AreEqual("custom", horizontal.Type);
        Assert.AreEqual(GridGuid, horizontal.Uuid);
        Assert.IsTrue(horizontal.ShowSpacing);
        Assert.AreEqual(24, horizontal.Spacing);
        Assert.AreEqual(0, horizontal.ZoneCount);
        Assert.AreEqual(0, horizontal.SensitivityRadius);
        Assert.IsNull(state.Layouts.Defaults.Vertical);
    }

    [TestMethod]
    public void TestWithDiff_Success()
    {
        // Arrange
        var input = CreateInput(CreateSampleModel());

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsFalse(state.InDesiredState);
        CollectionAssert.AreEqual(new List<string> { LayoutsResourceObject.LayoutsJsonPropertyName }, diff);

        // Test must not modify the layouts
        Assert.IsFalse(Directory.Exists(_dataFolder));
    }

    [TestMethod]
    public void TestWithoutDiff_Success()
    {
        // Arrange
        WriteEditorFiles();
        var input = CreateInput(CreateSampleModel());

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(state.InDesiredState);
        CollectionAssert.AreEqual(new List<string>(), diff);
    }

    [TestMethod]
    public void Test_CustomOrderDiffers_InDesiredState()
    {
        // Arrange: the order of the custom layouts only affects the editor list
        WriteEditorFiles();
        var model = CreateSampleModel();
        model.Custom.Reverse();
        var input = CreateInput(model);

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(state.InDesiredState);
        CollectionAssert.AreEqual(new List<string>(), diff);
    }

    [TestMethod]
    public void Test_OmittedSections_NotCompared()
    {
        // Arrange
        WriteEditorFiles();
        var matching = CreateInput(new FzLayoutsModel { Hotkeys = [new() { Key = 1, LayoutId = GridGuid }] });
        var differing = CreateInput(new FzLayoutsModel { Hotkeys = [new() { Key = 2, LayoutId = GridGuid }] });

        // Act
        var matchingResult = ExecuteDscCommand<TestCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", matching);
        var differingResult = ExecuteDscCommand<TestCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", differing);
        var (matchingState, _) = matchingResult.OutputStateAndDiff<LayoutsResourceObject>();
        var (differingState, _) = differingResult.OutputStateAndDiff<LayoutsResourceObject>();

        // Assert
        Assert.IsTrue(matchingResult.Success);
        Assert.IsTrue(matchingState.InDesiredState);
        Assert.IsTrue(differingResult.Success);
        Assert.IsFalse(differingState.InDesiredState);
    }

    [DataTestMethod]
    [DataRow(/*lang=json,strict*/ """{}""", "'layouts' is required")]
    [DataRow(/*lang=json,strict*/ """{"layouts":null}""", "'layouts' is required")]
    [DataRow(/*lang=json,strict*/ """{"layouts":[]}""", "'layouts' must be an object")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":null}}""", "'layouts.custom' must be an array")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"hotkeys":[null]}}""", "'layouts.hotkeys[0]' must be an object")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"defaults":[]}}""", "'layouts.defaults' must be an object")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"defaults":{"horizonal":{}}}}""", "'layouts.defaults.horizonal' is not a valid property")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"foo":1}}""", "'layouts.foo' is not a valid property")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"hotkeys":[{"key":"one","layoutId":"$GRID_GUID"}]}}""", "could not be converted")]
    public void Set_MalformedInput_FailsAndLeavesFilesUntouched(string input, string expectedError)
    {
        // Arrange: a stored hotkey that a bad input must not erase
        WriteEditorFile(LayoutsFunctionData.LayoutHotkeysFileName, EditorLayoutHotkeysJson);

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", ReplaceTokens(input));
        var messages = result.Messages();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Error, messages[0].Level);
        StringAssert.StartsWith(messages[0].Message, GetResourceString("InvalidLayoutsError", string.Empty));
        StringAssert.Contains(messages[0].Message, expectedError);
        Assert.AreEqual(EditorLayoutHotkeysJson, ReadFile(LayoutsFunctionData.LayoutHotkeysFileName));
    }

    [DataTestMethod]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","grid":$VALID_GRID,"canvas":{"refWidth":1,"refHeight":1,"zones":[{"x":0,"y":0,"width":1,"height":1}]}}]}}""", "custom[0] must set exactly one of 'canvas' or 'grid'")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A"}]}}""", "custom[0] must set exactly one of 'canvas' or 'grid'")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"not-a-guid","name":"A","grid":$VALID_GRID}]}}""", "custom[0].uuid: 'not-a-guid' is not a valid GUID")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"","grid":$VALID_GRID}]}}""", "custom[0].name is required")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","grid":$VALID_GRID},{"uuid":"$GRID_GUID","name":"B","grid":$VALID_GRID}]}}""", "custom[1].uuid: layout '$GRID_GUID' is defined more than once")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","grid":{"rows":1,"columns":2,"rowsPercentage":[10000],"columnsPercentage":[6000,3000],"cellChildMap":[[0,1]]}}]}}""", "custom[0].grid.columnsPercentage must sum to 10000 (100.00%) but sums to 9000")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","grid":{"rows":1,"columns":2,"rowsPercentage":[5000,5000],"columnsPercentage":[7000,3000],"cellChildMap":[[0,1]]}}]}}""", "custom[0].grid.rowsPercentage must contain 1 values, one per row")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","grid":{"rows":1,"columns":2,"rowsPercentage":[10000],"columnsPercentage":[7000,3000],"cellChildMap":[[0]]}}]}}""", "custom[0].grid.cellChildMap[0] must contain 2 values")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","grid":{"rows":1,"columns":2,"rowsPercentage":[10000],"columnsPercentage":[7000,3000],"cellChildMap":[[0,2]]}}]}}""", "custom[0].grid.cellChildMap[0][1]: zone index 2 is out of range (0-1)")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","canvas":{"refWidth":1920,"refHeight":1080,"zones":[]}}]}}""", "custom[0].canvas.zones must contain at least one zone")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","canvas":{"refWidth":1920,"refHeight":1080,"zones":[{"x":0,"y":0,"width":0,"height":100}]}}]}}""", "custom[0].canvas.zones[0].width must be greater than 0")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"hotkeys":[{"key":10,"layoutId":"$GRID_GUID"}]}}""", "hotkeys[0].key must be between 0 and 9")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"hotkeys":[{"key":1,"layoutId":"$GRID_GUID"},{"key":1,"layoutId":"$CANVAS_GUID"}]}}""", "hotkeys[1].key: key 1 is assigned more than once")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"hotkeys":[{"key":1,"layoutId":"$GRID_GUID"},{"key":2,"layoutId":"$GRID_GUID"}]}}""", "hotkeys[1].layoutId: layout '$GRID_GUID' is assigned more than one key")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"templates":[{"type":"hexagons"}]}}""", "templates[0].type: invalid value 'hexagons'; allowed values are: blank, focus, rows, columns, grid, priority-grid")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"templates":[{"type":"focus"},{"type":"focus"}]}}""", "templates[1].type: template 'focus' is defined more than once")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"templates":[{"type":"grid","zoneCount":0}]}}""", "templates[0].zoneCount must be greater than 0")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"defaults":{"horizontal":{"type":"custom"}}}}""", "defaults.horizontal.uuid is required when type is 'custom'")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"defaults":{"vertical":{"type":"rows","uuid":"$GRID_GUID"}}}}""", "defaults.vertical.uuid must only be set when type is 'custom'")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"defaults":{"vertical":{"type":"hexagons"}}}}""", "defaults.vertical.type: invalid value 'hexagons'; allowed values are: blank, focus, rows, columns, grid, priority-grid, custom")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","grid":$VALID_GRID}],"hotkeys":[{"key":1,"layoutId":"$CANVAS_GUID"}]}}""", "hotkeys[0].layoutId: layout '$CANVAS_GUID' is not defined in 'custom'")]
    [DataRow(/*lang=json,strict*/ """{"layouts":{"custom":[{"uuid":"$GRID_GUID","name":"A","grid":$VALID_GRID}],"defaults":{"horizontal":{"type":"custom","uuid":"$CANVAS_GUID"}}}}""", "defaults.horizontal.uuid: layout '$CANVAS_GUID' is not defined in 'custom'")]
    public void Set_InvalidLayouts_FailsAndLeavesFilesUntouched(string input, string expectedError)
    {
        // Arrange: a stored hotkey that a bad input must not erase
        WriteEditorFile(LayoutsFunctionData.LayoutHotkeysFileName, EditorLayoutHotkeysJson);

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", ReplaceTokens(input));
        var errors = result.Messages().Where(m => m.Level == DscMessageLevel.Error).Select(m => m.Message).ToList();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(errors.Count > 0, "Expected at least one error message");
        Assert.IsTrue(errors.Any(e => e.Contains(ReplaceTokens(expectedError))), $"Expected an error containing '{ReplaceTokens(expectedError)}' but got: {string.Join(" | ", errors)}");
        Assert.AreEqual(EditorLayoutHotkeysJson, ReadFile(LayoutsFunctionData.LayoutHotkeysFileName));
        Assert.IsFalse(File.Exists(Path.Combine(_dataFolder, LayoutsFunctionData.CustomLayoutsFileName)));
    }

    [TestMethod]
    public void Set_HotkeyToUnknownLayoutOnDisk_Warns()
    {
        // Arrange: no custom layouts on disk and none in the input
        var input = CreateInput(new FzLayoutsModel { Hotkeys = [new() { Key = 1, LayoutId = GridGuid }] });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", input);
        var messages = result.Messages();

        // Assert: the layout may be provisioned separately, so this is a warning only
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Warning, messages[0].Level);
        StringAssert.Contains(messages[0].Message, $"hotkeys[0].layoutId: layout '{GridGuid}' does not exist in the current custom layouts");
        Assert.IsTrue(File.Exists(Path.Combine(_dataFolder, LayoutsFunctionData.LayoutHotkeysFileName)));
    }

    [TestMethod]
    public void Set_NoSections_WarnsAndWritesNothing()
    {
        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module, "--input", /*lang=json,strict*/ """{"layouts":{}}""");
        var (_, diff) = result.OutputStateAndDiff<LayoutsResourceObject>();
        var messages = result.Messages();

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<string>(), diff);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Warning, messages[0].Level);
        StringAssert.Contains(messages[0].Message, "no layout sections are specified");
        Assert.IsFalse(Directory.Exists(_dataFolder));
    }

    [TestMethod]
    public void Get_MalformedFile_WarnsAndTreatsAsEmpty()
    {
        // Arrange
        WriteEditorFile(LayoutsFunctionData.CustomLayoutsFileName, "{ this is not json");
        WriteEditorFile(LayoutsFunctionData.LayoutHotkeysFileName, EditorLayoutHotkeysJson);

        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", LayoutsResource.ResourceName, "--module", Module);
        var state = result.OutputState<LayoutsResourceObject>();
        var messages = result.Messages();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, state.Layouts.Custom.Count);
        Assert.AreEqual(1, state.Layouts.Hotkeys.Count);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Warning, messages[0].Level);
        StringAssert.Contains(messages[0].Message, $"{LayoutsFunctionData.CustomLayoutsFileName} is malformed");
    }

    [TestMethod]
    public void Get_CustomLayoutWithoutInfo_SkippedWithWarning()
    {
        // Arrange: an entry the engine would skip, next to a valid one
        var json = /*lang=json,strict*/ """
            {
              "custom-layouts": [
                { "uuid": "{8F0B6D3E-2C41-4A5B-9D7E-0F1A2B3C4D5E}", "name": "Broken", "type": "grid" },
                { "uuid": "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}", "name": "Two columns", "type": "grid", "info": { "rows": 1, "columns": 2, "rows-percentage": [ 10000 ], "columns-percentage": [ 7000, 3000 ], "cell-child-map": [ [ 0, 1 ] ] } }
              ]
            }
            """;
        WriteEditorFile(LayoutsFunctionData.CustomLayoutsFileName, json);

        // Act
        var result = ExecuteDscCommand<ExportCommand>("--resource", LayoutsResource.ResourceName, "--module", Module);
        var state = result.OutputState<LayoutsResourceObject>();
        var messages = result.Messages();

        // Assert: the good entry is exported with the engine defaults filled in
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, state.Layouts.Custom.Count);
        Assert.AreEqual(GridGuid, state.Layouts.Custom[0].Uuid);
        Assert.IsTrue(state.Layouts.Custom[0].Grid.ShowSpacing);
        Assert.AreEqual(16, state.Layouts.Custom[0].Grid.Spacing);
        Assert.AreEqual(20, state.Layouts.Custom[0].Grid.SensitivityRadius);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Warning, messages[0].Level);
        StringAssert.Contains(messages[0].Message, $"Skipping custom layout '{CanvasGuid}' without layout info");
    }

    /// <summary>
    /// Creates the sample friendly model matching the editor-shaped files.
    /// </summary>
    private static FzLayoutsModel CreateSampleModel()
    {
        return new FzLayoutsModel
        {
            Custom =
            [
                CreateGridLayout(GridGuid),
                new()
                {
                    Uuid = CanvasGuid,
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
                },
            ],
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

    private static string CreateInput(FzLayoutsModel layouts)
    {
        return JsonSerializer.Serialize(new LayoutsResourceObject { Layouts = layouts });
    }

    private static string ReplaceTokens(string value)
    {
        return value
            .Replace(ValidGridToken, ValidGridJson)
            .Replace(GridGuidToken, GridGuid)
            .Replace(CanvasGuidToken, CanvasGuid);
    }

    private static void AssertLayoutsAreEqual(FzLayoutsModel expected, FzLayoutsModel actual)
    {
        var expectedJson = JsonSerializer.SerializeToNode(expected);
        var actualJson = JsonSerializer.SerializeToNode(actual);
        Assert.IsTrue(JsonNode.DeepEquals(expectedJson, actualJson), $"{expectedJson} != {actualJson}");
    }

    private void WriteEditorFiles()
    {
        WriteEditorFile(LayoutsFunctionData.CustomLayoutsFileName, EditorCustomLayoutsJson);
        WriteEditorFile(LayoutsFunctionData.LayoutTemplatesFileName, EditorLayoutTemplatesJson);
        WriteEditorFile(LayoutsFunctionData.LayoutHotkeysFileName, EditorLayoutHotkeysJson);
        WriteEditorFile(LayoutsFunctionData.DefaultLayoutsFileName, EditorDefaultLayoutsJson);
    }

    private void WriteEditorFile(string fileName, string json)
    {
        Directory.CreateDirectory(_dataFolder);
        File.WriteAllText(Path.Combine(_dataFolder, fileName), json);
    }

    private string ReadFile(string fileName)
    {
        return File.ReadAllText(Path.Combine(_dataFolder, fileName));
    }
}
