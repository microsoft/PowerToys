// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FancyZonesEditorCommon.Data
{
    public static class Constants
    {
        public enum TemplateLayout
        {
            Empty,
            Focus,
            Rows,
            Columns,
            Grid,
            PriorityGrid,
        }

        public static readonly ReadOnlyDictionary<TemplateLayout, string> TemplateLayoutJsonTags = new ReadOnlyDictionary<TemplateLayout, string>(
            new Dictionary<TemplateLayout, string>()
            {
                { TemplateLayout.Empty, "blank" },
                { TemplateLayout.Focus, "focus" },
                { TemplateLayout.Rows, "rows" },
                { TemplateLayout.Columns, "columns" },
                { TemplateLayout.Grid, "grid" },
                { TemplateLayout.PriorityGrid, "priority-grid" },
            });

        public const string CustomLayoutJsonTag = "custom";
    }
}
