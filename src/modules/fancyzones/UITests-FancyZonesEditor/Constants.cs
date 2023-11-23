// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.FancyZonesEditor.UITests
{
    public static class Constants
    {
        public enum TemplateLayouts
        {
            Empty,
            Focus,
            Rows,
            Columns,
            Grid,
            PriorityGrid,
        }

        public static readonly Dictionary<TemplateLayouts, string> TemplateLayoutNames = new Dictionary<TemplateLayouts, string>()
        {
            { TemplateLayouts.Empty, "No layout" },
            { TemplateLayouts.Focus, "Focus" },
            { TemplateLayouts.Rows, "Rows" },
            { TemplateLayouts.Columns, "Columns" },
            { TemplateLayouts.Grid, "Grid" },
            { TemplateLayouts.PriorityGrid, "Priority Grid" },
        };

        public static readonly Dictionary<TemplateLayouts, string> TemplateLayoutTypes = new Dictionary<TemplateLayouts, string>()
        {
            { TemplateLayouts.Empty, "blank" },
            { TemplateLayouts.Focus, "focus" },
            { TemplateLayouts.Rows, "rows" },
            { TemplateLayouts.Columns, "columns" },
            { TemplateLayouts.Grid, "grid" },
            { TemplateLayouts.PriorityGrid, "priority-grid" },
        };
    }
}
