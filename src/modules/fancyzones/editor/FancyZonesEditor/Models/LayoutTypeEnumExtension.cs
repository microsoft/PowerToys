// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor.Models
{
    public static class LayoutTypeEnumExtension
    {
        private const string BlankJsonTag = "blank";
        private const string FocusJsonTag = "focus";
        private const string RowsJsonTag = "rows";
        private const string ColumnsJsonTag = "columns";
        private const string GridJsonTag = "grid";
        private const string PriorityGridJsonTag = "priority-grid";
        private const string CustomLayoutJsonTag = "custom";

        public static string TypeToString(this LayoutType value)
        {
            switch (value)
            {
                case LayoutType.Blank:
                    return BlankJsonTag;
                case LayoutType.Focus:
                    return FocusJsonTag;
                case LayoutType.Rows:
                    return RowsJsonTag;
                case LayoutType.Columns:
                    return ColumnsJsonTag;
                case LayoutType.Grid:
                    return GridJsonTag;
                case LayoutType.PriorityGrid:
                    return PriorityGridJsonTag;
            }

            return CustomLayoutJsonTag;
        }

        public static LayoutType TypeFromString(string value)
        {
            switch (value)
            {
                case BlankJsonTag:
                    return LayoutType.Blank;
                case FocusJsonTag:
                    return LayoutType.Focus;
                case RowsJsonTag:
                    return LayoutType.Rows;
                case ColumnsJsonTag:
                    return LayoutType.Columns;
                case GridJsonTag:
                    return LayoutType.Grid;
                case PriorityGridJsonTag:
                    return LayoutType.PriorityGrid;
            }

            return LayoutType.Custom;
        }
    }
}
