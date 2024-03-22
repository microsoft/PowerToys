// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using static FancyZonesEditorCommon.Data.Constants;

namespace Microsoft.FancyZonesEditor.UITests
{
    public static class TestConstants
    {
        public static readonly Dictionary<TemplateLayout, string> TemplateLayoutNames = new Dictionary<TemplateLayout, string>()
        {
            { TemplateLayout.Empty, "No layout" },
            { TemplateLayout.Focus, "Focus" },
            { TemplateLayout.Rows, "Rows" },
            { TemplateLayout.Columns, "Columns" },
            { TemplateLayout.Grid, "Grid" },
            { TemplateLayout.PriorityGrid, "PriorityGrid" },
        };
    }
}
