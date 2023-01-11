// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;

namespace ManagedCommon
{
    public static class ColorNameHelper
    {
        public const float DBLEPSILON = 2.2204460492503131e-16f;

        // For the purposes of naming colors, there are four steps that we go through.
        //
        // 1. For numerical ease, we convert the HSL values from the range [0, 1]
        // to the range [0, 255].
        //
        // 2. If luminosity is sufficiently high or low (> 240 or < 20), or if
        // saturation is sufficiently low (<= 20), then we declare that we're in the
        // achromatic range.  In this case, we return either white, black, or three
        // different shades of gray (depending on luminosity).
        //
        // 3. If we do have a chromatic color, the first thing we need to determine
        // about it is what the hue limits are for its saturation value - at different
        // levels of saturation, we have different hue values that we'll consider the
        // boundaries for different classes of named colors.  The hue limits for various
        // saturation values are as below.
        //
        // The numbers correspond to the following color buckets, with 0 meaning that
        // that bucket does not apply to the given saturation value:
        //
        // 1 - coral, 2 - red, 3 - orange, 4 - brown, 5 - tan, 6 - gold, 7 - yellow, 8 - olive green (with brown),
        // 9 - olive green (with green) 10 - lime green, 11 - green - 12 - bright green 13 - teal, 14 - aqua,
        // 15 - turquoise, 16 - pale blue, 17 - blue, 18 - blue-gray, 19 - indigo, 20 - purple, 21 - pink, 22 - brown, 23 - red
        private static int[] hueLimitsForSatLevel1 = // Sat: 20-75
        {
        // 1,  2,  3,  4,  5,  6,  7,  8,  9,  10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20,  21,  22,  23
            8,  0,  0, 44,  0,  0,  0, 63,  0,   0, 122,   0, 134,   0,   0,   0,   0, 166, 176, 241,   0, 256,   0,
        };

        private static int[] hueLimitsForSatLevel2 = // Sat: 75-115
        {
        // 1,  2,  3,  4,  5,  6,  7,  8,  9,  10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20,  21,  22,  23
            0, 10,  0, 32, 46,  0,  0,  0, 61,   0, 106,   0, 136, 144,   0,   0,   0, 158, 166, 241,   0,   0, 256,
        };

        private static int[] hueLimitsForSatLevel3 = // Sat: 115-150
        {
        // 1,  2,  3,  4,  5,  6,  7,  8,  9,  10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20,  21,  22,  23
            0,  8,  0,  0, 39, 46,  0,  0,  0,  71, 120,   0, 131, 144,   0,   0, 163,   0, 177, 211, 249,   0, 256,
        };

        private static int[] hueLimitsForSatLevel4 = // Sat: 150-240
        {
        // 1,  2,  3,  4,  5,  6,  7,  8,  9,  10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20,  21,  22,  23
            0, 11, 26,  0,  0, 38, 45,  0,  0,  56, 100, 121, 129,   0, 140,   0, 180,   0,   0, 224, 241,   0, 256,
        };

        private static int[] hueLimitsForSatLevel5 = // Sat: 240-255
        {
        // 1,  2,  3,  4,  5,  6,  7,  8,  9,  10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20,  21,  22,  23
            0, 13, 27,  0,  0, 36, 45,  0,  0,  59, 118,   0, 127, 136, 142,   0, 185,   0,   0, 216, 239,   0, 256,
        };

        // 4. Once we have the color bucket, next we have three sub-buckets that we need to worry about,
        // corresponding to three different levels of luminosity.  For example, if we're in the "blue" bucket,
        // that might correspond to light blue, blue, or dark blue, depending on luminosity.
        // For each bucket, the luminosity cutoffs for the purposes of discerning between light, mid, and dark colors
        // are different, so we define luminosity limits for low and high luminosity for each bucket, as follows:
        private static int[] lumLimitsForHueIndexLow =
        {
        // 1,   2,   3,   4,   5,   6,   7,    8,  9,    10, 11,  12,  13,  14,   15, 16,  17,  18,  19,  20,  21,  22,  23
            130, 100, 115, 100, 100, 100, 110,  75, 100,  90, 100, 100, 100, 100,  80, 100, 100, 100, 100, 100, 100, 100, 100,
        };

        private static int[] lumLimitsForHueIndexHigh =
        {
        // 1,   2,   3,   4,   5,   6,   7,   8,   9,   10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20,  21,  22,  23
            170, 170, 170, 155, 170, 170, 170, 170, 170, 115, 170, 170, 170, 170, 170, 170, 170, 170, 150, 150, 170, 140, 165,
        };

        // 5. Finally, once we have a luminosity sub-bucket in the saturation color bucket, we have everything we need
        // to retrieve a name.  For each of the 23 buckets, we have names associated with light, mid, and dark variations
        // of that color, which are defined as follows:
        private static string[] colorNamesLight =
        {
            "TEXT_COLOR_CORAL",
            "TEXT_COLOR_ROSE",
            "TEXT_COLOR_LIGHTORANGE",
            "TEXT_COLOR_TAN",
            "TEXT_COLOR_TAN",
            "TEXT_COLOR_LIGHTYELLOW",
            "TEXT_COLOR_LIGHTYELLOW",
            "TEXT_COLOR_TAN",
            "TEXT_COLOR_LIGHTGREEN",
            "TEXT_COLOR_LIME",
            "TEXT_COLOR_LIGHTGREEN",
            "TEXT_COLOR_LIGHTGREEN",
            "TEXT_COLOR_AQUA",
            "TEXT_COLOR_SKYBLUE",
            "TEXT_COLOR_LIGHTTURQUOISE",
            "TEXT_COLOR_PALEBLUE",
            "TEXT_COLOR_LIGHTBLUE",
            "TEXT_COLOR_ICEBLUE",
            "TEXT_COLOR_PERIWINKLE",
            "TEXT_COLOR_LAVENDER",
            "TEXT_COLOR_PINK",
            "TEXT_COLOR_TAN",
            "TEXT_COLOR_ROSE",
        };

        private static string[] colorNamesMid =
        {
            "TEXT_COLOR_CORAL",
            "TEXT_COLOR_RED",
            "TEXT_COLOR_ORANGE",
            "TEXT_COLOR_BROWN",
            "TEXT_COLOR_TAN",
            "TEXT_COLOR_GOLD",
            "TEXT_COLOR_YELLOW",
            "TEXT_COLOR_OLIVEGREEN",
            "TEXT_COLOR_OLIVEGREEN",
            "TEXT_COLOR_GREEN",
            "TEXT_COLOR_GREEN",
            "TEXT_COLOR_BRIGHTGREEN",
            "TEXT_COLOR_TEAL",
            "TEXT_COLOR_AQUA",
            "TEXT_COLOR_TURQUOISE",
            "TEXT_COLOR_PALEBLUE",
            "TEXT_COLOR_BLUE",
            "TEXT_COLOR_BLUEGRAY",
            "TEXT_COLOR_INDIGO",
            "TEXT_COLOR_PURPLE",
            "TEXT_COLOR_PINK",
            "TEXT_COLOR_BROWN",
            "TEXT_COLOR_RED",
        };

        private static string[] colorNamesDark =
        {
            "TEXT_COLOR_BROWN",
            "TEXT_COLOR_DARKRED",
            "TEXT_COLOR_BROWN",
            "TEXT_COLOR_BROWN",
            "TEXT_COLOR_BROWN",
            "TEXT_COLOR_DARKYELLOW",
            "TEXT_COLOR_DARKYELLOW",
            "TEXT_COLOR_BROWN",
            "TEXT_COLOR_DARKGREEN",
            "TEXT_COLOR_DARKGREEN",
            "TEXT_COLOR_DARKGREEN",
            "TEXT_COLOR_DARKGREEN",
            "TEXT_COLOR_DARKTEAL",
            "TEXT_COLOR_DARKTEAL",
            "TEXT_COLOR_DARKTEAL",
            "TEXT_COLOR_DARKBLUE",
            "TEXT_COLOR_DARKBLUE",
            "TEXT_COLOR_BLUEGRAY",
            "TEXT_COLOR_INDIGO",
            "TEXT_COLOR_DARKPURPLE",
            "TEXT_COLOR_PLUM",
            "TEXT_COLOR_BROWN",
            "TEXT_COLOR_DARKRED",
        };

        public static string GetColorNameIdentifier(Color color)
        {
            var (hue, sat, lum) = ColorFormatHelper.ConvertToHSLColor(color);

            hue = (hue == 0 ? 0 : hue / 360) * 255; // this implementation is using normalization to 0-255 instead of 0-360°
            sat = sat * 255;
            lum = lum * 255;

            // First, if we're in the achromatic state, return the appropriate achromatic color name.
            if (lum > 240)
            {
                return "TEXT_COLOR_WHITE";
            }
            else if (lum < 20)
            {
                return "TEXT_COLOR_BLACK";
            }

            if (sat <= 20)
            {
                if (lum > 170)
                {
                    return "TEXT_COLOR_LIGHTGRAY";
                }
                else if (lum > 100)
                {
                    return "TEXT_COLOR_GRAY";
                }
                else
                {
                    return "TEXT_COLOR_DARKGRAY";
                }
            }

            // If we have a chromatic color, we need to first get the hue limits for the saturation value.
            int[] pHueLimits;
            if (sat > 20 && sat <= 75)
            {
                pHueLimits = hueLimitsForSatLevel1;
            }
            else if (sat > 75 && sat <= 115)
            {
                pHueLimits = hueLimitsForSatLevel2;
            }
            else if (sat > 115 && sat <= 150)
            {
                pHueLimits = hueLimitsForSatLevel3;
            }
            else if (sat > 150 && sat <= 240)
            {
                pHueLimits = hueLimitsForSatLevel4;
            }
            else
            {
                pHueLimits = hueLimitsForSatLevel5;
            }

            // Now that we have that, we can get the color index, which represents which
            // of the 23 buckets we're located in.
            int colorIndex = -1;
            for (int i = 0; i < colorNamesMid.Length; ++i)
            {
                if (hue < pHueLimits[i])
                {
                    colorIndex = i;
                    break;
                }
            }

            // Assuming we got a color index (and we always should get one), then next we need to
            // figure out which luminosity sub-bucket we're located in.
            // Once we have that, we'll return the color name from the appropriate array.
            if (colorIndex != -1)
            {
                if (lum > lumLimitsForHueIndexHigh[colorIndex])
                {
                    return colorNamesLight[colorIndex];
                }
                else if (lum < lumLimitsForHueIndexLow[colorIndex])
                {
                    return colorNamesDark[colorIndex];
                }
                else
                {
                    return colorNamesMid[colorIndex];
                }
            }

            return string.Empty;
        }

        public static bool AreClose(double a, double b)
        {
            return (float)Math.Abs(a - b) <= DBLEPSILON * (float)Math.Abs(a);
        }
    }
}
