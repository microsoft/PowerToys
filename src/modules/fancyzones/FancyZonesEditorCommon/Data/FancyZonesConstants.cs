// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FancyZonesEditorCommon.Data;

/// <summary>
/// Constants used across FancyZones components.
/// </summary>
public static class FancyZonesConstants
{
    public enum TemplateLayoutType
    {
        Empty,
        Focus,
        Rows,
        Columns,
        Grid,
        PriorityGrid,
    }

    public static readonly ReadOnlyDictionary<TemplateLayoutType, string> TemplateLayoutJsonTags = new ReadOnlyDictionary<TemplateLayoutType, string>(
        new Dictionary<TemplateLayoutType, string>()
        {
            { TemplateLayoutType.Empty, "blank" },
            { TemplateLayoutType.Focus, "focus" },
            { TemplateLayoutType.Rows, "rows" },
            { TemplateLayoutType.Columns, "columns" },
            { TemplateLayoutType.Grid, "grid" },
            { TemplateLayoutType.PriorityGrid, "priority-grid" },
        });

    public const string CustomLayoutJsonTag = "custom";

    // Layout type strings
    public const string LayoutTypeFocus = "focus";
    public const string LayoutTypeRows = "rows";
    public const string LayoutTypeColumns = "columns";
    public const string LayoutTypeGrid = "grid";
    public const string LayoutTypePriorityGrid = "priority-grid";
    public const string LayoutTypeCustom = "custom";
    public const string LayoutTypeBlank = "blank";
}
