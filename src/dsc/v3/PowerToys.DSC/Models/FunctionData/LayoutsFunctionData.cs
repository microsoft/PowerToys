// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FancyZonesEditorCommon.Data;
using PowerToys.DSC.Models.FancyZones;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.Models.FunctionData;

/// <summary>
/// Function data for the FancyZones layouts DSC resource. Reads and writes
/// the FancyZones layout data files. A running FancyZones instance watches
/// these files and reloads them when they change, so no signal is needed.
/// </summary>
public sealed class LayoutsFunctionData : BaseFunctionData
{
    public const string CustomLayoutsFileName = "custom-layouts.json";
    public const string LayoutTemplatesFileName = "layout-templates.json";
    public const string LayoutHotkeysFileName = "layout-hotkeys.json";
    public const string DefaultLayoutsFileName = "default-layouts.json";

    // Structural problems with the input JSON (missing or mistyped members)
    // detected before the model is materialized.
    private readonly IList<string> _inputErrors;

    /// <summary>
    /// Gets or sets the resolver of the folder holding the FancyZones data
    /// files. Defaults to the FancyZones folder in the PowerToys local
    /// application data; tests replace it with a temporary folder.
    /// </summary>
    public static Func<string> DataFolder { get; set; } = GetDefaultDataFolder;

    /// <summary>
    /// Gets the desired state provided as input, if any.
    /// </summary>
    public LayoutsResourceObject Input { get; }

    /// <summary>
    /// Gets the current state read from the layout files.
    /// </summary>
    public LayoutsResourceObject Output { get; }

    /// <summary>
    /// Gets the warnings collected while reading the current state and
    /// validating the input.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    public LayoutsFunctionData(string? input = null)
    {
        Output = new();
        Input = new();
        _inputErrors = [];

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        // Deserialization does not enforce [Required] or non-nullable
        // annotations; check the shape of the JSON before materializing the
        // model so that e.g. a null section cannot erase a file on Set.
        var node = JsonNode.Parse(input);
        _inputErrors = ValidateInputStructure(node);
        if (_inputErrors.Count == 0)
        {
            Input = node.Deserialize<LayoutsResourceObject>() ?? new();
        }
    }

    /// <summary>
    /// Validates the input layouts. Call after <see cref="GetState"/> so that
    /// references to existing custom layouts can be checked.
    /// </summary>
    /// <returns>The list of validation errors; empty when the input is valid.</returns>
    public IList<string> ValidateInput()
    {
        return _inputErrors.Count > 0 ? _inputErrors : FzLayoutsConverter.Validate(Input.Layouts, Output.Layouts, Warnings);
    }

    /// <summary>
    /// Reads the current layout files into the output state. A missing file
    /// is an empty section; a malformed file is reported as a warning and
    /// treated as empty, mirroring how the FancyZones engine loads it.
    /// </summary>
    public void GetState()
    {
        var layouts = Output.Layouts;
        layouts.Custom = FzLayoutsConverter.FromCustomLayouts(ReadFile(new CustomLayouts(), CustomLayoutsFileName), Warnings);
        layouts.Templates = FzLayoutsConverter.FromLayoutTemplates(ReadFile(new LayoutTemplates(), LayoutTemplatesFileName), Warnings);
        layouts.Hotkeys = FzLayoutsConverter.FromLayoutHotkeys(ReadFile(new LayoutHotkeys(), LayoutHotkeysFileName), Warnings);
        layouts.Defaults = FzLayoutsConverter.FromDefaultLayouts(ReadFile(new DefaultLayouts(), DefaultLayoutsFileName), Warnings);
    }

    /// <summary>
    /// Writes the sections of the desired state that differ from the current
    /// state to their layout files. Each write replaces the whole file.
    /// </summary>
    public void SetState()
    {
        var desired = Input.Layouts;
        var sections = GetDifferingSections();

        if (sections.Contains(FzLayoutsModel.CustomJsonPropertyName))
        {
            WriteFile(new CustomLayouts(), CustomLayoutsFileName, FzLayoutsConverter.ToCustomLayouts(desired.Custom!));
        }

        if (sections.Contains(FzLayoutsModel.TemplatesJsonPropertyName))
        {
            WriteFile(new LayoutTemplates(), LayoutTemplatesFileName, FzLayoutsConverter.ToLayoutTemplates(desired.Templates!));
        }

        if (sections.Contains(FzLayoutsModel.HotkeysJsonPropertyName))
        {
            WriteFile(new LayoutHotkeys(), LayoutHotkeysFileName, FzLayoutsConverter.ToLayoutHotkeys(desired.Hotkeys!));
        }

        if (sections.Contains(FzLayoutsModel.DefaultsJsonPropertyName))
        {
            WriteFile(new DefaultLayouts(), DefaultLayoutsFileName, FzLayoutsConverter.ToDefaultLayouts(desired.Defaults!, KnownCustomLayouts));
        }
    }

    /// <summary>
    /// Tests whether every section of the desired state matches the current
    /// state. Sections that are not part of the desired state are not compared.
    /// </summary>
    /// <returns>True if the states match; otherwise false.</returns>
    public bool TestState()
    {
        return GetDifferingSections().Count == 0;
    }

    /// <summary>
    /// Gets the difference between the desired and the current state.
    /// </summary>
    /// <returns>A JSON array with the differing property names.</returns>
    public JsonArray GetDiffJson()
    {
        var diff = new JsonArray();
        if (!TestState())
        {
            diff.Add(LayoutsResourceObject.LayoutsJsonPropertyName);
        }

        return diff;
    }

    /// <summary>
    /// Gets the state after the desired state has been applied: the current
    /// state with every section of the desired state replaced by its
    /// canonical form.
    /// </summary>
    /// <returns>The merged canonical state.</returns>
    public FzLayoutsModel GetDesiredState()
    {
        var desired = FzLayoutsConverter.Canonicalize(Input.Layouts, KnownCustomLayouts);
        var current = Output.Layouts;
        return new FzLayoutsModel
        {
            Custom = desired.Custom ?? current.Custom,
            Templates = desired.Templates ?? current.Templates,
            Hotkeys = desired.Hotkeys ?? current.Hotkeys,
            Defaults = desired.Defaults ?? current.Defaults,
        };
    }

    /// <summary>
    /// Gets the schema for the layouts resource object.
    /// </summary>
    /// <returns>The JSON schema string.</returns>
    public string Schema()
    {
        return GenerateSchema<LayoutsResourceObject>();
    }

    /// <summary>
    /// Gets the custom layouts a default layout may reference: the desired
    /// custom layouts when they are part of the input, otherwise the current
    /// ones.
    /// </summary>
    private IReadOnlyList<FzCustomLayout> KnownCustomLayouts => Input.Layouts.Custom ?? Output.Layouts.Custom ?? [];

    private static string GetDefaultDataFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "FancyZones");
    }

    private static void WriteFile<T>(EditorData<T> data, string fileName, T value)
    {
        var folder = DataFolder();
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, fileName), data.Serialize(value));
    }

    private static bool AreEqual<T>(T expected, T actual)
    {
        return JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected), JsonSerializer.SerializeToNode(actual));
    }

    private static List<FzCustomLayout> SortByUuid(List<FzCustomLayout> layouts)
    {
        return layouts.OrderBy(layout => layout.Uuid, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Checks that the input JSON has the required shape: a "layouts" object
    /// whose optional "custom", "templates" and "hotkeys" members are arrays
    /// of objects and whose optional "defaults" member is an object with
    /// optional "horizontal" and "vertical" objects. Unknown members are
    /// rejected so that a misspelled section cannot be silently ignored. Type
    /// mismatches inside the entries are left to the deserializer.
    /// </summary>
    /// <param name="node">The parsed input JSON.</param>
    /// <returns>The list of structural errors; empty when the shape is valid.</returns>
    private static IList<string> ValidateInputStructure(JsonNode? node)
    {
        var errors = new List<string>();
        if (node is not JsonObject root)
        {
            errors.Add("input must be a JSON object");
            return errors;
        }

        if (!root.TryGetPropertyValue(LayoutsResourceObject.LayoutsJsonPropertyName, out var layoutsNode) || layoutsNode == null)
        {
            errors.Add($"'{LayoutsResourceObject.LayoutsJsonPropertyName}' is required");
            return errors;
        }

        if (layoutsNode is not JsonObject layouts)
        {
            errors.Add($"'{LayoutsResourceObject.LayoutsJsonPropertyName}' must be an object");
            return errors;
        }

        foreach (var (name, value) in layouts)
        {
            var context = $"{LayoutsResourceObject.LayoutsJsonPropertyName}.{name}";
            switch (name)
            {
                case FzLayoutsModel.CustomJsonPropertyName:
                case FzLayoutsModel.TemplatesJsonPropertyName:
                case FzLayoutsModel.HotkeysJsonPropertyName:
                    ValidateArrayOfObjects(value, context, errors);
                    break;
                case FzLayoutsModel.DefaultsJsonPropertyName:
                    ValidateDefaultsStructure(value, context, errors);
                    break;
                default:
                    errors.Add($"'{context}' is not a valid property; allowed properties are: {FzLayoutsModel.CustomJsonPropertyName}, {FzLayoutsModel.TemplatesJsonPropertyName}, {FzLayoutsModel.HotkeysJsonPropertyName}, {FzLayoutsModel.DefaultsJsonPropertyName}");
                    break;
            }
        }

        return errors;
    }

    private static void ValidateArrayOfObjects(JsonNode? value, string context, IList<string> errors)
    {
        if (value is not JsonArray array)
        {
            errors.Add($"'{context}' must be an array");
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject)
            {
                errors.Add($"'{context}[{i.ToString(CultureInfo.InvariantCulture)}]' must be an object");
            }
        }
    }

    private static void ValidateDefaultsStructure(JsonNode? value, string context, IList<string> errors)
    {
        if (value is not JsonObject defaults)
        {
            errors.Add($"'{context}' must be an object");
            return;
        }

        foreach (var (name, layout) in defaults)
        {
            var layoutContext = $"{context}.{name}";
            if (name is not (FzDefaultLayouts.HorizontalJsonPropertyName or FzDefaultLayouts.VerticalJsonPropertyName))
            {
                errors.Add($"'{layoutContext}' is not a valid property; allowed properties are: {FzDefaultLayouts.HorizontalJsonPropertyName}, {FzDefaultLayouts.VerticalJsonPropertyName}");
                continue;
            }

            if (layout is not JsonObject)
            {
                errors.Add($"'{layoutContext}' must be an object");
            }
        }
    }

    /// <summary>
    /// Reads a layout file. A missing file yields the default (empty) wrapper;
    /// a malformed file is reported as a warning and also yields the default.
    /// </summary>
    private T ReadFile<T>(EditorData<T> data, string fileName)
        where T : struct
    {
        var path = Path.Combine(DataFolder(), fileName);
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            return data.Read(path);
        }
        catch (JsonException ex)
        {
            Warnings.Add($"{fileName} is malformed and is treated as empty: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Gets the names of the sections of the desired state whose canonical
    /// form differs from the current state. Custom layouts are compared
    /// regardless of their order, as the order only affects the editor list.
    /// </summary>
    private List<string> GetDifferingSections()
    {
        var desired = FzLayoutsConverter.Canonicalize(Input.Layouts, KnownCustomLayouts);
        var current = Output.Layouts;
        var sections = new List<string>();

        if (desired.Custom != null && !AreEqual(SortByUuid(desired.Custom), SortByUuid(current.Custom ?? [])))
        {
            sections.Add(FzLayoutsModel.CustomJsonPropertyName);
        }

        if (desired.Templates != null && !AreEqual(desired.Templates, current.Templates ?? []))
        {
            sections.Add(FzLayoutsModel.TemplatesJsonPropertyName);
        }

        if (desired.Hotkeys != null && !AreEqual(desired.Hotkeys, current.Hotkeys ?? []))
        {
            sections.Add(FzLayoutsModel.HotkeysJsonPropertyName);
        }

        if (desired.Defaults != null && !AreEqual(desired.Defaults, current.Defaults ?? new()))
        {
            sections.Add(FzLayoutsModel.DefaultsJsonPropertyName);
        }

        return sections;
    }
}
