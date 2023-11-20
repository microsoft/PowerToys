// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.FancyZonesEditor.UITests
{
    public static class Constants
    {
        public enum Layouts
        {
            Empty,
            Focus,
            Rows,
            Columns,
            Grid,
            PriorityGrid,
        }

        public static readonly Dictionary<Layouts, string> LayoutNames = new Dictionary<Layouts, string>()
        {
            { Layouts.Empty, "No layout" },
            { Layouts.Focus, "Focus" },
            { Layouts.Rows, "Rows" },
            { Layouts.Columns, "Columns" },
            { Layouts.Grid, "Grid" },
            { Layouts.PriorityGrid, "PriorityGrid" },
        };

        public static readonly Dictionary<Layouts, string> LayoutTypes = new Dictionary<Layouts, string>()
        {
            { Layouts.Empty, "blank" },
            { Layouts.Focus, "focus" },
            { Layouts.Rows, "rows" },
            { Layouts.Columns, "columns" },
            { Layouts.Grid, "grid" },
            { Layouts.PriorityGrid, "priority-grid" },
        };
    }
}
