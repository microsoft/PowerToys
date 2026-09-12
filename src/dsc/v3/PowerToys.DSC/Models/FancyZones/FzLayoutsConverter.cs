// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using FancyZonesEditorCommon.Data;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// Converts between the friendly <see cref="FzLayoutsModel"/> used by the DSC
/// layouts resource and the FancyZones data file shapes defined in
/// FancyZonesEditorCommon (custom-layouts.json, layout-templates.json,
/// layout-hotkeys.json and default-layouts.json).
/// </summary>
public static class FzLayoutsConverter
{
    /// <summary>
    /// The layout type of a default layout that references a custom layout.
    /// </summary>
    public const string CustomLayoutType = Constants.CustomLayoutJsonTag;

    public const string HorizontalMonitorConfiguration = "horizontal";
    public const string VerticalMonitorConfiguration = "vertical";

    // Row and column sizes are stored in hundredths of a percent.
    private const int PercentageTotal = 10000;

    // Template layout type names in editor order (blank, focus, rows, columns, grid, priority-grid).
    private static readonly string[] _templateTypes = Enum.GetValues<Constants.TemplateLayout>()
        .Select(type => Constants.TemplateLayoutJsonTags[type])
        .ToArray();

    // Template types whose zones are separated by spacing; the engine ignores
    // spacing for the blank and focus templates (Layout::Init).
    private static readonly string[] _gridTemplateTypes =
    [
        Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows],
        Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns],
        Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid],
        Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid],
    ];

    private static readonly string[] _defaultLayoutTypes = [.. _templateTypes, CustomLayoutType];

    private static readonly string _canvasType = CustomLayout.Canvas.TypeToString();
    private static readonly string _gridType = CustomLayout.Grid.TypeToString();

    /// <summary>
    /// Validates the friendly model and returns the list of validation
    /// errors; an empty list means the model is valid. The messages are
    /// intentionally not localized: they quote JSON property paths and values
    /// that must match the configuration document verbatim, and are presented
    /// inside the localized InvalidLayoutsError message frame.
    /// </summary>
    /// <param name="model">The friendly model to validate.</param>
    /// <param name="current">The current state, used to check references to
    /// existing custom layouts when the model does not define custom layouts.</param>
    /// <param name="warnings">Optional collector for warnings.</param>
    /// <returns>The list of validation errors.</returns>
    public static IList<string> Validate(FzLayoutsModel model, FzLayoutsModel? current = null, IList<string>? warnings = null)
    {
        var errors = new List<string>();

        if (model.Custom == null && model.Templates == null && model.Hotkeys == null && model.Defaults == null)
        {
            warnings?.Add("no layout sections are specified; nothing is managed");
        }

        ValidateCustomLayouts(model.Custom, errors);
        ValidateTemplates(model.Templates, errors);
        ValidateHotkeys(model.Hotkeys, model, current, errors, warnings);
        ValidateDefaults(model.Defaults, model, current, errors, warnings);

        return errors;
    }

    /// <summary>
    /// Normalizes a GUID string to the form written by the FancyZones editor:
    /// upper-case with braces, e.g. "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}".
    /// The engine parses layout identifiers with CLSIDFromString, which
    /// requires the braces.
    /// </summary>
    /// <param name="value">The GUID string, with or without braces.</param>
    /// <param name="normalized">The normalized GUID string.</param>
    /// <returns>True if the value is a valid GUID; otherwise false.</returns>
    public static bool TryNormalizeGuid(string? value, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value.Trim(), out var guid))
        {
            return false;
        }

        normalized = guid.ToString("B").ToUpperInvariant();
        return true;
    }

    /// <summary>
    /// Converts validated custom layouts to the custom-layouts.json shape.
    /// </summary>
    /// <param name="layouts">The custom layouts; must have passed <see cref="Validate"/>.</param>
    /// <returns>The stored custom layouts.</returns>
    public static CustomLayouts.CustomLayoutListWrapper ToCustomLayouts(List<FzCustomLayout> layouts)
    {
        var serializer = new CustomLayouts();
        var stored = new List<CustomLayouts.CustomLayoutWrapper>(layouts.Count);

        foreach (var entry in layouts)
        {
            var wrapper = new CustomLayouts.CustomLayoutWrapper
            {
                Uuid = NormalizeGuidOrThrow(entry.Uuid),
                Name = entry.Name,
            };

            if (entry.Grid != null)
            {
                wrapper.Type = _gridType;
                wrapper.Info = serializer.ToJsonElement(new CustomLayouts.GridInfoWrapper
                {
                    Rows = entry.Grid.Rows,
                    Columns = entry.Grid.Columns,
                    RowsPercentage = [.. entry.Grid.RowsPercentage ?? []],
                    ColumnsPercentage = [.. entry.Grid.ColumnsPercentage ?? []],
                    CellChildMap = (entry.Grid.CellChildMap ?? []).Select(row => (row ?? []).ToArray()).ToArray(),
                    ShowSpacing = entry.Grid.ShowSpacing ?? LayoutDefaultSettings.DefaultShowSpacing,
                    Spacing = entry.Grid.Spacing ?? LayoutDefaultSettings.DefaultSpacing,
                    SensitivityRadius = entry.Grid.SensitivityRadius ?? LayoutDefaultSettings.DefaultSensitivityRadius,
                });
            }
            else if (entry.Canvas != null)
            {
                wrapper.Type = _canvasType;
                wrapper.Info = serializer.ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                {
                    RefWidth = entry.Canvas.RefWidth,
                    RefHeight = entry.Canvas.RefHeight,
                    Zones = (entry.Canvas.Zones ?? []).Select(zone => new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                    {
                        X = zone.X,
                        Y = zone.Y,
                        Width = zone.Width,
                        Height = zone.Height,
                    }).ToList(),
                    SensitivityRadius = entry.Canvas.SensitivityRadius ?? LayoutDefaultSettings.DefaultSensitivityRadius,
                });
            }
            else
            {
                throw new InvalidOperationException($"Custom layout '{entry.Uuid}' must set exactly one of 'canvas' or 'grid'");
            }

            stored.Add(wrapper);
        }

        return new CustomLayouts.CustomLayoutListWrapper { CustomLayouts = stored };
    }

    /// <summary>
    /// Converts stored custom layouts to the canonical friendly model. Entries
    /// the engine would not load are skipped with a warning.
    /// </summary>
    /// <param name="stored">The stored custom layouts.</param>
    /// <param name="warnings">Optional collector for warnings about skipped entries.</param>
    /// <returns>The canonical custom layouts, in stored order.</returns>
    public static List<FzCustomLayout> FromCustomLayouts(CustomLayouts.CustomLayoutListWrapper stored, IList<string>? warnings = null)
    {
        var serializer = new CustomLayouts();
        var result = new List<FzCustomLayout>();

        foreach (var layout in stored.CustomLayouts ?? [])
        {
            if (!TryNormalizeGuid(layout.Uuid, out var uuid))
            {
                warnings?.Add($"Skipping custom layout '{layout.Name}' with invalid uuid '{layout.Uuid}'");
                continue;
            }

            if (layout.Info.ValueKind != JsonValueKind.Object)
            {
                warnings?.Add($"Skipping custom layout '{uuid}' without layout info");
                continue;
            }

            var entry = new FzCustomLayout
            {
                Uuid = uuid,
                Name = layout.Name ?? string.Empty,
            };

            try
            {
                if (string.Equals(layout.Type, _gridType, StringComparison.Ordinal))
                {
                    var info = serializer.GridFromJsonElement(layout.Info.GetRawText());
                    entry.Grid = new FzGridInfo
                    {
                        Rows = info.Rows,
                        Columns = info.Columns,
                        RowsPercentage = [.. info.RowsPercentage ?? []],
                        ColumnsPercentage = [.. info.ColumnsPercentage ?? []],
                        CellChildMap = (info.CellChildMap ?? []).Select(row => (row ?? []).ToList()).ToList(),
                        ShowSpacing = info.ShowSpacing,
                        Spacing = info.Spacing,
                        SensitivityRadius = info.SensitivityRadius,
                    };
                }
                else if (string.Equals(layout.Type, _canvasType, StringComparison.Ordinal))
                {
                    var info = serializer.CanvasFromJsonElement(layout.Info.GetRawText());
                    entry.Canvas = new FzCanvasInfo
                    {
                        RefWidth = info.RefWidth,
                        RefHeight = info.RefHeight,
                        Zones = (info.Zones ?? []).Select(zone => new FzCanvasZone
                        {
                            X = zone.X,
                            Y = zone.Y,
                            Width = zone.Width,
                            Height = zone.Height,
                        }).ToList(),
                        SensitivityRadius = info.SensitivityRadius,
                    };
                }
                else
                {
                    warnings?.Add($"Skipping custom layout '{uuid}' with unknown type '{layout.Type}'");
                    continue;
                }
            }
            catch (JsonException ex)
            {
                warnings?.Add($"Skipping custom layout '{uuid}' with malformed layout info: {ex.Message}");
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Converts validated layout templates to the layout-templates.json shape.
    /// </summary>
    /// <param name="templates">The templates; must have passed <see cref="Validate"/>.</param>
    /// <returns>The stored templates, in editor order.</returns>
    public static LayoutTemplates.TemplateLayoutsListWrapper ToLayoutTemplates(List<FzTemplateLayout> templates)
    {
        var stored = templates
            .Select(template => new LayoutTemplates.TemplateLayoutWrapper
            {
                Type = template.Type,
                ZoneCount = template.ZoneCount ?? LayoutDefaultSettings.DefaultZoneCount,
                SensitivityRadius = template.SensitivityRadius ?? LayoutDefaultSettings.DefaultSensitivityRadius,
                ShowSpacing = IsGridTemplateType(template.Type) && (template.ShowSpacing ?? LayoutDefaultSettings.DefaultShowSpacing),
                Spacing = IsGridTemplateType(template.Type) ? template.Spacing ?? LayoutDefaultSettings.DefaultSpacing : 0,
            })
            .OrderBy(template => TemplateOrder(template.Type))
            .ToList();

        return new LayoutTemplates.TemplateLayoutsListWrapper { LayoutTemplates = stored };
    }

    /// <summary>
    /// Converts stored layout templates to the canonical friendly model.
    /// </summary>
    /// <param name="stored">The stored templates.</param>
    /// <param name="warnings">Optional collector for warnings about skipped entries.</param>
    /// <returns>The canonical templates, in editor order.</returns>
    public static List<FzTemplateLayout> FromLayoutTemplates(LayoutTemplates.TemplateLayoutsListWrapper stored, IList<string>? warnings = null)
    {
        var result = new List<FzTemplateLayout>();

        foreach (var template in stored.LayoutTemplates ?? [])
        {
            if (!_templateTypes.Contains(template.Type, StringComparer.Ordinal))
            {
                warnings?.Add($"Skipping layout template with unknown type '{template.Type}'");
                continue;
            }

            result.Add(new FzTemplateLayout
            {
                Type = template.Type,
                ZoneCount = template.ZoneCount,
                ShowSpacing = template.ShowSpacing,
                Spacing = template.Spacing,
                SensitivityRadius = template.SensitivityRadius,
            });
        }

        return result.OrderBy(template => TemplateOrder(template.Type)).ToList();
    }

    /// <summary>
    /// Converts validated layout hotkeys to the layout-hotkeys.json shape.
    /// </summary>
    /// <param name="hotkeys">The hotkeys; must have passed <see cref="Validate"/>.</param>
    /// <returns>The stored hotkeys, sorted by key.</returns>
    public static LayoutHotkeys.LayoutHotkeysWrapper ToLayoutHotkeys(List<FzLayoutHotkey> hotkeys)
    {
        var stored = hotkeys
            .Select(hotkey => new LayoutHotkeys.LayoutHotkeyWrapper
            {
                Key = hotkey.Key,
                LayoutId = NormalizeGuidOrThrow(hotkey.LayoutId),
            })
            .OrderBy(hotkey => hotkey.Key)
            .ToList();

        return new LayoutHotkeys.LayoutHotkeysWrapper { LayoutHotkeys = stored };
    }

    /// <summary>
    /// Converts stored layout hotkeys to the canonical friendly model.
    /// </summary>
    /// <param name="stored">The stored hotkeys.</param>
    /// <param name="warnings">Optional collector for warnings about skipped entries.</param>
    /// <returns>The canonical hotkeys, sorted by key.</returns>
    public static List<FzLayoutHotkey> FromLayoutHotkeys(LayoutHotkeys.LayoutHotkeysWrapper stored, IList<string>? warnings = null)
    {
        var result = new List<FzLayoutHotkey>();

        foreach (var hotkey in stored.LayoutHotkeys ?? [])
        {
            if (!TryNormalizeGuid(hotkey.LayoutId, out var uuid))
            {
                warnings?.Add($"Skipping layout hotkey {Invariant(hotkey.Key)} with invalid layout id '{hotkey.LayoutId}'");
                continue;
            }

            result.Add(new FzLayoutHotkey
            {
                Key = hotkey.Key,
                LayoutId = uuid,
            });
        }

        return result.OrderBy(hotkey => hotkey.Key).ToList();
    }

    /// <summary>
    /// Converts validated default layouts to the default-layouts.json shape.
    /// </summary>
    /// <param name="defaults">The default layouts; must have passed <see cref="Validate"/>.</param>
    /// <param name="customLayouts">The custom layouts a default layout may reference,
    /// used to copy the spacing settings of a referenced grid layout the way the editor does.</param>
    /// <returns>The stored default layouts.</returns>
    public static DefaultLayouts.DefaultLayoutsListWrapper ToDefaultLayouts(FzDefaultLayouts defaults, IReadOnlyList<FzCustomLayout> customLayouts)
    {
        var stored = new List<DefaultLayouts.DefaultLayoutWrapper>();
        AddDefaultLayout(stored, HorizontalMonitorConfiguration, defaults.Horizontal, customLayouts);
        AddDefaultLayout(stored, VerticalMonitorConfiguration, defaults.Vertical, customLayouts);
        return new DefaultLayouts.DefaultLayoutsListWrapper { DefaultLayouts = stored };
    }

    /// <summary>
    /// Converts stored default layouts to the canonical friendly model.
    /// </summary>
    /// <param name="stored">The stored default layouts.</param>
    /// <param name="warnings">Optional collector for warnings about skipped entries.</param>
    /// <returns>The canonical default layouts.</returns>
    public static FzDefaultLayouts FromDefaultLayouts(DefaultLayouts.DefaultLayoutsListWrapper stored, IList<string>? warnings = null)
    {
        var result = new FzDefaultLayouts();

        foreach (var defaultLayout in stored.DefaultLayouts ?? [])
        {
            var layout = defaultLayout.Layout;
            var context = $"default layout for '{defaultLayout.MonitorConfiguration}' monitors";

            if (!_defaultLayoutTypes.Contains(layout.Type, StringComparer.Ordinal))
            {
                warnings?.Add($"Skipping {context} with unknown type '{layout.Type}'");
                continue;
            }

            var entry = new FzDefaultLayout
            {
                Type = layout.Type,
                ZoneCount = layout.ZoneCount,
                ShowSpacing = layout.ShowSpacing,
                Spacing = layout.Spacing,
                SensitivityRadius = layout.SensitivityRadius,
            };

            if (IsCustomType(layout.Type))
            {
                if (!TryNormalizeGuid(layout.Uuid, out var uuid))
                {
                    warnings?.Add($"Skipping {context} with invalid uuid '{layout.Uuid}'");
                    continue;
                }

                entry.Uuid = uuid;
            }

            // The engine treats every monitor configuration other than
            // "vertical" as horizontal (DefaultLayoutsJsonUtils::TypeFromString).
            if (string.Equals(defaultLayout.MonitorConfiguration, VerticalMonitorConfiguration, StringComparison.Ordinal))
            {
                result.Vertical = entry;
            }
            else
            {
                result.Horizontal = entry;
            }
        }

        return result;
    }

    /// <summary>
    /// Normalizes a friendly model into its canonical form: normalized
    /// identifiers, defaults filled in, and entries sorted. Sections that are
    /// not set stay unset. Used to compare desired and current state.
    /// </summary>
    /// <param name="model">The friendly model; must have passed <see cref="Validate"/>.</param>
    /// <param name="customLayouts">The current custom layouts, used to resolve
    /// default layout references when the model does not define custom layouts.</param>
    /// <returns>The canonical friendly model.</returns>
    public static FzLayoutsModel Canonicalize(FzLayoutsModel model, IReadOnlyList<FzCustomLayout>? customLayouts = null)
    {
        // Round-tripping through the stored shape guarantees that the desired
        // state and the state read back from disk normalize identically.
        IReadOnlyList<FzCustomLayout> knownCustomLayouts = model.Custom ?? customLayouts ?? [];

        return new FzLayoutsModel
        {
            Custom = model.Custom == null ? null : FromCustomLayouts(ToCustomLayouts(model.Custom)),
            Templates = model.Templates == null ? null : FromLayoutTemplates(ToLayoutTemplates(model.Templates)),
            Hotkeys = model.Hotkeys == null ? null : FromLayoutHotkeys(ToLayoutHotkeys(model.Hotkeys)),
            Defaults = model.Defaults == null ? null : FromDefaultLayouts(ToDefaultLayouts(model.Defaults, knownCustomLayouts)),
        };
    }

    private static void AddDefaultLayout(List<DefaultLayouts.DefaultLayoutWrapper> stored, string monitorConfiguration, FzDefaultLayout? layout, IReadOnlyList<FzCustomLayout> customLayouts)
    {
        if (layout == null)
        {
            return;
        }

        DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper layoutWrapper;
        if (IsCustomType(layout.Type))
        {
            // Mirror FancyZonesEditorIO.SerializeDefaultLayouts: a custom
            // default layout carries the spacing settings of the referenced
            // grid layout and the struct defaults for everything else.
            var uuid = NormalizeGuidOrThrow(layout.Uuid);
            var grid = customLayouts.FirstOrDefault(custom => custom != null && TryNormalizeGuid(custom.Uuid, out var id) && string.Equals(id, uuid, StringComparison.Ordinal))?.Grid;
            layoutWrapper = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
            {
                Uuid = uuid,
                Type = CustomLayoutType,
                ZoneCount = layout.ZoneCount ?? 0,
                SensitivityRadius = layout.SensitivityRadius ?? 0,
                ShowSpacing = layout.ShowSpacing ?? (grid != null && (grid.ShowSpacing ?? LayoutDefaultSettings.DefaultShowSpacing)),
                Spacing = layout.Spacing ?? (grid != null ? grid.Spacing ?? LayoutDefaultSettings.DefaultSpacing : 0),
            };
        }
        else
        {
            var isGrid = IsGridTemplateType(layout.Type);
            layoutWrapper = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
            {
                Uuid = string.Empty,
                Type = layout.Type,
                ZoneCount = layout.ZoneCount ?? LayoutDefaultSettings.DefaultZoneCount,
                SensitivityRadius = layout.SensitivityRadius ?? LayoutDefaultSettings.DefaultSensitivityRadius,
                ShowSpacing = isGrid && (layout.ShowSpacing ?? LayoutDefaultSettings.DefaultShowSpacing),
                Spacing = isGrid ? layout.Spacing ?? LayoutDefaultSettings.DefaultSpacing : 0,
            };
        }

        stored.Add(new DefaultLayouts.DefaultLayoutWrapper
        {
            MonitorConfiguration = monitorConfiguration,
            Layout = layoutWrapper,
        });
    }

    private static void ValidateCustomLayouts(List<FzCustomLayout>? layouts, IList<string> errors)
    {
        if (layouts == null)
        {
            return;
        }

        var seenUuids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < layouts.Count; i++)
        {
            var entry = layouts[i];
            var context = $"custom[{Invariant(i)}]";
            if (entry == null)
            {
                errors.Add($"{context} must be an object");
                continue;
            }

            ValidateGuid(entry.Uuid, $"{context}.uuid", errors, out var uuid);
            if (uuid != null && !seenUuids.Add(uuid))
            {
                errors.Add($"{context}.uuid: layout '{uuid}' is defined more than once");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                errors.Add($"{context}.name is required");
            }

            var kinds = (entry.Canvas != null ? 1 : 0) + (entry.Grid != null ? 1 : 0);
            if (kinds != 1)
            {
                errors.Add($"{context} must set exactly one of 'canvas' or 'grid'");
            }

            if (entry.Canvas != null)
            {
                ValidateCanvas(entry.Canvas, $"{context}.canvas", errors);
            }

            if (entry.Grid != null)
            {
                ValidateGrid(entry.Grid, $"{context}.grid", errors);
            }
        }
    }

    private static void ValidateCanvas(FzCanvasInfo canvas, string context, IList<string> errors)
    {
        if (canvas.RefWidth <= 0)
        {
            errors.Add($"{context}.refWidth must be greater than 0");
        }

        if (canvas.RefHeight <= 0)
        {
            errors.Add($"{context}.refHeight must be greater than 0");
        }

        var zones = canvas.Zones ?? [];
        if (zones.Count == 0)
        {
            errors.Add($"{context}.zones must contain at least one zone");
        }
        else if (zones.Count > LayoutDefaultSettings.MaxZones)
        {
            errors.Add($"{context}.zones must not contain more than {Invariant(LayoutDefaultSettings.MaxZones)} zones");
        }

        for (var i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            var zoneContext = $"{context}.zones[{Invariant(i)}]";
            if (zone == null)
            {
                errors.Add($"{zoneContext} must be an object");
                continue;
            }

            if (zone.Width <= 0)
            {
                errors.Add($"{zoneContext}.width must be greater than 0");
            }

            if (zone.Height <= 0)
            {
                errors.Add($"{zoneContext}.height must be greater than 0");
            }
        }

        ValidateNotNegative(canvas.SensitivityRadius, $"{context}.sensitivityRadius", errors);
    }

    private static void ValidateGrid(FzGridInfo grid, string context, IList<string> errors)
    {
        var dimensionsValid = true;
        if (grid.Rows <= 0)
        {
            errors.Add($"{context}.rows must be greater than 0");
            dimensionsValid = false;
        }

        if (grid.Columns <= 0)
        {
            errors.Add($"{context}.columns must be greater than 0");
            dimensionsValid = false;
        }

        ValidateNotNegative(grid.Spacing, $"{context}.spacing", errors);
        ValidateNotNegative(grid.SensitivityRadius, $"{context}.sensitivityRadius", errors);

        if (!dimensionsValid)
        {
            return;
        }

        ValidatePercentages(grid.RowsPercentage, grid.Rows, "row", $"{context}.rowsPercentage", errors);
        ValidatePercentages(grid.ColumnsPercentage, grid.Columns, "column", $"{context}.columnsPercentage", errors);

        var cellChildMap = grid.CellChildMap ?? [];
        var mapContext = $"{context}.cellChildMap";
        if (cellChildMap.Count != grid.Rows)
        {
            errors.Add($"{mapContext} must contain {Invariant(grid.Rows)} rows");
            return;
        }

        var maxZoneIndex = (grid.Rows * grid.Columns) - 1;
        for (var row = 0; row < cellChildMap.Count; row++)
        {
            var cells = cellChildMap[row];
            var rowContext = $"{mapContext}[{Invariant(row)}]";
            if (cells == null || cells.Count != grid.Columns)
            {
                errors.Add($"{rowContext} must contain {Invariant(grid.Columns)} values");
                continue;
            }

            for (var column = 0; column < cells.Count; column++)
            {
                if (cells[column] < 0 || cells[column] > maxZoneIndex)
                {
                    errors.Add($"{rowContext}[{Invariant(column)}]: zone index {Invariant(cells[column])} is out of range (0-{Invariant(maxZoneIndex)})");
                }
            }
        }
    }

    private static void ValidatePercentages(List<int>? values, int expectedCount, string dimension, string context, IList<string> errors)
    {
        values ??= [];
        if (values.Count != expectedCount)
        {
            errors.Add($"{context} must contain {Invariant(expectedCount)} values, one per {dimension}");
            return;
        }

        var sum = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] <= 0)
            {
                errors.Add($"{context}[{Invariant(i)}] must be greater than 0");
            }

            sum += values[i];
        }

        if (sum != PercentageTotal)
        {
            errors.Add($"{context} must sum to {Invariant(PercentageTotal)} (100.00%) but sums to {Invariant(sum)}");
        }
    }

    private static void ValidateTemplates(List<FzTemplateLayout>? templates, IList<string> errors)
    {
        if (templates == null)
        {
            return;
        }

        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < templates.Count; i++)
        {
            var entry = templates[i];
            var context = $"templates[{Invariant(i)}]";
            if (entry == null)
            {
                errors.Add($"{context} must be an object");
                continue;
            }

            if (ValidateLayoutType(entry.Type, _templateTypes, $"{context}.type", errors) && !seenTypes.Add(entry.Type))
            {
                errors.Add($"{context}.type: template '{entry.Type}' is defined more than once");
            }

            ValidateZoneCount(entry.ZoneCount, entry.Type, $"{context}.zoneCount", errors);
            ValidateNotNegative(entry.Spacing, $"{context}.spacing", errors);
            ValidateNotNegative(entry.SensitivityRadius, $"{context}.sensitivityRadius", errors);
        }
    }

    private static void ValidateHotkeys(List<FzLayoutHotkey>? hotkeys, FzLayoutsModel model, FzLayoutsModel? current, IList<string> errors, IList<string>? warnings)
    {
        if (hotkeys == null)
        {
            return;
        }

        var seenKeys = new HashSet<int>();
        var seenLayouts = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < hotkeys.Count; i++)
        {
            var entry = hotkeys[i];
            var context = $"hotkeys[{Invariant(i)}]";
            if (entry == null)
            {
                errors.Add($"{context} must be an object");
                continue;
            }

            if (entry.Key < 0 || entry.Key > 9)
            {
                errors.Add($"{context}.key must be between 0 and 9");
            }
            else if (!seenKeys.Add(entry.Key))
            {
                errors.Add($"{context}.key: key {Invariant(entry.Key)} is assigned more than once");
            }

            ValidateGuid(entry.LayoutId, $"{context}.layoutId", errors, out var uuid);
            if (uuid == null)
            {
                continue;
            }

            if (!seenLayouts.Add(uuid))
            {
                errors.Add($"{context}.layoutId: layout '{uuid}' is assigned more than one key");
            }

            ValidateLayoutReference(uuid, $"{context}.layoutId", model, current, errors, warnings);
        }
    }

    private static void ValidateDefaults(FzDefaultLayouts? defaults, FzLayoutsModel model, FzLayoutsModel? current, IList<string> errors, IList<string>? warnings)
    {
        if (defaults == null)
        {
            return;
        }

        ValidateDefaultLayout(defaults.Horizontal, $"{FzLayoutsModel.DefaultsJsonPropertyName}.{FzDefaultLayouts.HorizontalJsonPropertyName}", model, current, errors, warnings);
        ValidateDefaultLayout(defaults.Vertical, $"{FzLayoutsModel.DefaultsJsonPropertyName}.{FzDefaultLayouts.VerticalJsonPropertyName}", model, current, errors, warnings);
    }

    private static void ValidateDefaultLayout(FzDefaultLayout? layout, string context, FzLayoutsModel model, FzLayoutsModel? current, IList<string> errors, IList<string>? warnings)
    {
        if (layout == null)
        {
            return;
        }

        if (ValidateLayoutType(layout.Type, _defaultLayoutTypes, $"{context}.type", errors))
        {
            if (IsCustomType(layout.Type))
            {
                if (string.IsNullOrWhiteSpace(layout.Uuid))
                {
                    errors.Add($"{context}.uuid is required when type is '{CustomLayoutType}'");
                }
                else
                {
                    ValidateGuid(layout.Uuid, $"{context}.uuid", errors, out var uuid);
                    if (uuid != null)
                    {
                        ValidateLayoutReference(uuid, $"{context}.uuid", model, current, errors, warnings);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(layout.Uuid))
            {
                errors.Add($"{context}.uuid must only be set when type is '{CustomLayoutType}'");
            }
        }

        ValidateZoneCount(layout.ZoneCount, layout.Type, $"{context}.zoneCount", errors);
        ValidateNotNegative(layout.Spacing, $"{context}.spacing", errors);
        ValidateNotNegative(layout.SensitivityRadius, $"{context}.sensitivityRadius", errors);
    }

    /// <summary>
    /// Checks that a referenced custom layout exists. When the model defines
    /// the custom layouts itself the reference must resolve within them; when
    /// it does not, a reference to a layout missing from the current custom
    /// layouts is reported as a warning only, because the layout may be
    /// provisioned by another resource or run.
    /// </summary>
    private static void ValidateLayoutReference(string uuid, string context, FzLayoutsModel model, FzLayoutsModel? current, IList<string> errors, IList<string>? warnings)
    {
        if (model.Custom != null)
        {
            if (!ContainsLayout(model.Custom, uuid))
            {
                errors.Add($"{context}: layout '{uuid}' is not defined in '{FzLayoutsModel.CustomJsonPropertyName}'");
            }
        }
        else if (current?.Custom != null && !ContainsLayout(current.Custom, uuid))
        {
            warnings?.Add($"{context}: layout '{uuid}' does not exist in the current custom layouts");
        }
    }

    private static bool ContainsLayout(List<FzCustomLayout> layouts, string uuid)
    {
        return layouts.Any(layout => layout != null && TryNormalizeGuid(layout.Uuid, out var id) && string.Equals(id, uuid, StringComparison.Ordinal));
    }

    private static void ValidateGuid(string? value, string context, IList<string> errors, out string? uuid)
    {
        uuid = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{context} is required");
            return;
        }

        if (!TryNormalizeGuid(value, out var normalized))
        {
            errors.Add($"{context}: '{value}' is not a valid GUID");
            return;
        }

        uuid = normalized;
    }

    private static bool ValidateLayoutType(string? type, string[] allowedTypes, string context, IList<string> errors)
    {
        if (string.IsNullOrEmpty(type))
        {
            errors.Add($"{context} is required");
            return false;
        }

        if (!allowedTypes.Contains(type, StringComparer.Ordinal))
        {
            errors.Add($"{context}: invalid value '{type}'; allowed values are: {string.Join(", ", allowedTypes)}");
            return false;
        }

        return true;
    }

    private static void ValidateZoneCount(int? zoneCount, string? type, string context, IList<string> errors)
    {
        if (zoneCount == null)
        {
            return;
        }

        // The engine rejects a zone count of 0 for the grid-based templates (Layout::Init)
        if (IsGridTemplateType(type))
        {
            if (zoneCount <= 0)
            {
                errors.Add($"{context} must be greater than 0");
            }
        }
        else if (zoneCount < 0)
        {
            errors.Add($"{context} must not be negative");
        }
    }

    private static void ValidateNotNegative(int? value, string context, IList<string> errors)
    {
        if (value < 0)
        {
            errors.Add($"{context} must not be negative");
        }
    }

    private static bool IsGridTemplateType(string? type)
    {
        return type != null && _gridTemplateTypes.Contains(type, StringComparer.Ordinal);
    }

    private static bool IsCustomType(string? type)
    {
        return string.Equals(type, CustomLayoutType, StringComparison.Ordinal);
    }

    private static int TemplateOrder(string? type)
    {
        return Array.IndexOf(_templateTypes, type ?? string.Empty);
    }

    private static string NormalizeGuidOrThrow(string? value)
    {
        if (!TryNormalizeGuid(value, out var normalized))
        {
            throw new InvalidOperationException($"'{value}' is not a valid GUID");
        }

        return normalized;
    }

    private static string Invariant(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
