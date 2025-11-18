// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditor.Models;

using static FancyZonesEditorCommon.Data.Constants;

namespace Microsoft.FancyZonesEditor.UITests
{
    public static class TestConstants
    {
        public static readonly Dictionary<LayoutType, string> TemplateLayoutNames = new Dictionary<LayoutType, string>()
        {
            { LayoutType.Blank, "No layout" },
            { LayoutType.Focus, "Focus" },
            { LayoutType.Rows, "Rows" },
            { LayoutType.Columns, "Columns" },
            { LayoutType.Grid, "Grid" },
            { LayoutType.PriorityGrid, "Priority Grid" },
        };
    }
}
