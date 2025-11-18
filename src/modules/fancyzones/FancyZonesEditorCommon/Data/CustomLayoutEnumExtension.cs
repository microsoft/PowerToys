// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditorCommon.Data
{
    public enum CustomLayout
    {
        Canvas,
        Grid,
    }

    public static class CustomLayoutEnumExtension
    {
        private const string CanvasJsonTag = "canvas";
        private const string GridJsonTag = "grid";

        public static string TypeToString(this CustomLayout value)
        {
            switch (value)
            {
                case CustomLayout.Canvas:
                    return CanvasJsonTag;
                case CustomLayout.Grid:
                    return GridJsonTag;
            }

            return CanvasJsonTag;
        }

        public static CustomLayout TypeFromString(string value)
        {
            switch (value)
            {
                case CanvasJsonTag:
                    return CustomLayout.Canvas;
                case GridJsonTag:
                    return CustomLayout.Grid;
            }

            return CustomLayout.Canvas;
        }
    }
}
