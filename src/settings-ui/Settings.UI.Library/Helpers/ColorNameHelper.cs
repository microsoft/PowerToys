// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using global::Settings.UI.Library.Resources;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    public static class ColorNameHelper
    {
        public static string GetColorNameFromColorIdentifier(string colorIdentifier)
        {
            switch (colorIdentifier)
            {
                case "TEXT_COLOR_WHITE": return Resources.TEXT_COLOR_WHITE;
                case "TEXT_COLOR_BLACK": return Resources.TEXT_COLOR_BLACK;
                case "TEXT_COLOR_LIGHTGRAY": return Resources.TEXT_COLOR_LIGHTGRAY;
                case "TEXT_COLOR_GRAY": return Resources.TEXT_COLOR_GRAY;
                case "TEXT_COLOR_DARKGRAY": return Resources.TEXT_COLOR_DARKGRAY;
                case "TEXT_COLOR_CORAL": return Resources.TEXT_COLOR_CORAL;
                case "TEXT_COLOR_ROSE": return Resources.TEXT_COLOR_ROSE;
                case "TEXT_COLOR_LIGHTORANGE": return Resources.TEXT_COLOR_LIGHTORANGE;
                case "TEXT_COLOR_TAN": return Resources.TEXT_COLOR_TAN;
                case "TEXT_COLOR_LIGHTYELLOW": return Resources.TEXT_COLOR_LIGHTYELLOW;
                case "TEXT_COLOR_LIGHTGREEN": return Resources.TEXT_COLOR_LIGHTGREEN;
                case "TEXT_COLOR_LIME": return Resources.TEXT_COLOR_LIME;
                case "TEXT_COLOR_AQUA": return Resources.TEXT_COLOR_AQUA;
                case "TEXT_COLOR_SKYBLUE": return Resources.TEXT_COLOR_SKYBLUE;
                case "TEXT_COLOR_LIGHTTURQUOISE": return Resources.TEXT_COLOR_LIGHTTURQUOISE;
                case "TEXT_COLOR_PALEBLUE": return Resources.TEXT_COLOR_PALEBLUE;
                case "TEXT_COLOR_LIGHTBLUE": return Resources.TEXT_COLOR_LIGHTBLUE;
                case "TEXT_COLOR_ICEBLUE": return Resources.TEXT_COLOR_ICEBLUE;
                case "TEXT_COLOR_PERIWINKLE": return Resources.TEXT_COLOR_PERIWINKLE;
                case "TEXT_COLOR_LAVENDER": return Resources.TEXT_COLOR_LAVENDER;
                case "TEXT_COLOR_PINK": return Resources.TEXT_COLOR_PINK;
                case "TEXT_COLOR_RED": return Resources.TEXT_COLOR_RED;
                case "TEXT_COLOR_ORANGE": return Resources.TEXT_COLOR_ORANGE;
                case "TEXT_COLOR_BROWN": return Resources.TEXT_COLOR_BROWN;
                case "TEXT_COLOR_GOLD": return Resources.TEXT_COLOR_GOLD;
                case "TEXT_COLOR_YELLOW": return Resources.TEXT_COLOR_YELLOW;
                case "TEXT_COLOR_OLIVEGREEN": return Resources.TEXT_COLOR_OLIVEGREEN;
                case "TEXT_COLOR_GREEN": return Resources.TEXT_COLOR_GREEN;
                case "TEXT_COLOR_BRIGHTGREEN": return Resources.TEXT_COLOR_BRIGHTGREEN;
                case "TEXT_COLOR_TEAL": return Resources.TEXT_COLOR_TEAL;
                case "TEXT_COLOR_TURQUOISE": return Resources.TEXT_COLOR_TURQUOISE;
                case "TEXT_COLOR_BLUE": return Resources.TEXT_COLOR_BLUE;
                case "TEXT_COLOR_BLUEGRAY": return Resources.TEXT_COLOR_BLUEGRAY;
                case "TEXT_COLOR_INDIGO": return Resources.TEXT_COLOR_INDIGO;
                case "TEXT_COLOR_PURPLE": return Resources.TEXT_COLOR_PURPLE;
                case "TEXT_COLOR_DARKRED": return Resources.TEXT_COLOR_DARKRED;
                case "TEXT_COLOR_DARKYELLOW": return Resources.TEXT_COLOR_DARKYELLOW;
                case "TEXT_COLOR_DARKGREEN": return Resources.TEXT_COLOR_DARKGREEN;
                case "TEXT_COLOR_DARKTEAL": return Resources.TEXT_COLOR_DARKTEAL;
                case "TEXT_COLOR_DARKBLUE": return Resources.TEXT_COLOR_DARKBLUE;
                case "TEXT_COLOR_DARKPURPLE": return Resources.TEXT_COLOR_DARKPURPLE;
                case "TEXT_COLOR_PLUM": return Resources.TEXT_COLOR_PLUM;
                default: return colorIdentifier;
            }
        }

        public static string ReplaceName(string colorFormat, Color? colorOrNull)
        {
            Color color = (Color)(colorOrNull == null ? Color.Moccasin : colorOrNull);
            return colorFormat.Replace(ColorFormatHelper.GetColorNameParameter(), GetColorNameFromColorIdentifier(ManagedCommon.ColorNameHelper.GetColorNameIdentifier(color)));
        }
    }
}
