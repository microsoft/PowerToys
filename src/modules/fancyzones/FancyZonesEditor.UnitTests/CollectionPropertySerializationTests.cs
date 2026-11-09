// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using FancyZonesEditorCommon.Data;

namespace UnitTestsFancyZonesEditor;

[TestClass]
public class CollectionPropertySerializationTests
{
    [TestMethod]
    public void EditorDataCollectionsCanBeDeserialized()
    {
        const string appliedLayoutsJson = """
            { "applied-layouts": [] }
            """;
        const string customLayoutsJson = """
            { "custom-layouts": [] }
            """;
        const string defaultLayoutsJson = """
            { "default-layouts": [] }
            """;
        const string editorParametersJson = """
            { "process-id": 1, "span-zones-across-monitors": false, "monitors": [] }
            """;
        const string layoutHotkeysJson = """
            { "layout-hotkeys": [] }
            """;
        const string layoutTemplatesJson = """
            { "layout-templates": [] }
            """;

        Assert.IsNotNull(JsonSerializer.Deserialize(appliedLayoutsJson, FancyZonesJsonContext.Default.AppliedLayoutsListWrapper).AppliedLayouts);
        Assert.IsNotNull(JsonSerializer.Deserialize(customLayoutsJson, FancyZonesJsonContext.Default.CustomLayoutListWrapper).CustomLayouts);
        Assert.IsNotNull(JsonSerializer.Deserialize(defaultLayoutsJson, FancyZonesJsonContext.Default.DefaultLayoutsListWrapper).DefaultLayouts);
        Assert.IsNotNull(JsonSerializer.Deserialize(editorParametersJson, FancyZonesJsonContext.Default.ParamsWrapper).Monitors);
        Assert.IsNotNull(JsonSerializer.Deserialize(layoutHotkeysJson, FancyZonesJsonContext.Default.LayoutHotkeysWrapper).LayoutHotkeys);
        Assert.IsNotNull(JsonSerializer.Deserialize(layoutTemplatesJson, FancyZonesJsonContext.Default.TemplateLayoutsListWrapper).LayoutTemplates);
    }

    [TestMethod]
    public void CustomLayoutCollectionsCanBeDeserialized()
    {
        const string canvasInfoJson = """
            { "ref-width": 1920, "ref-height": 1080, "zones": [] }
            """;
        const string gridInfoJson = """
            {
              "rows": 1,
              "columns": 1,
              "rows-percentage": [10000],
              "columns-percentage": [10000],
              "cell-child-map": [[0]]
            }
            """;

        var canvasInfo = JsonSerializer.Deserialize(canvasInfoJson, FancyZonesJsonContext.Default.CanvasInfoWrapper);
        Assert.IsNotNull(canvasInfo);
        Assert.IsNotNull(canvasInfo.Zones);

        var gridInfo = JsonSerializer.Deserialize(gridInfoJson, FancyZonesJsonContext.Default.GridInfoWrapper);
        Assert.IsNotNull(gridInfo);
        Assert.IsNotNull(gridInfo.RowsPercentage);
        Assert.IsNotNull(gridInfo.ColumnsPercentage);
    }
}
