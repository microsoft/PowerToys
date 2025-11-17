// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Globalization;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    public static class SettingsUtilities
    {
        public static string ToRGBHex(string color)
        {
            if (color == null)
            {
                return "#FFFFFF";
            }

            // Using InvariantCulture as these are expected to be hex codes.
            bool success = int.TryParse(
                color.Replace("#", string.Empty),
                System.Globalization.NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out int argb);

            if (success)
            {
                Color clr = Color.FromArgb(argb);
                return "#" + clr.R.ToString("X2", CultureInfo.InvariantCulture) +
                    clr.G.ToString("X2", CultureInfo.InvariantCulture) +
                    clr.B.ToString("X2", CultureInfo.InvariantCulture);
            }
            else
            {
                return "#FFFFFF";
            }
        }

        public static string ToARGBHex(string color)
        {
            if (color == null)
            {
                return "#FFFFFFFF";
            }

            // Using InvariantCulture as these are expected to be hex codes.
            bool success = int.TryParse(
                color.Replace("#", string.Empty),
                System.Globalization.NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out int argb);

            if (success)
            {
                Color clr = Color.FromArgb(argb);
                return "#" + clr.A.ToString("X2", CultureInfo.InvariantCulture) +
                    clr.R.ToString("X2", CultureInfo.InvariantCulture) +
                    clr.G.ToString("X2", CultureInfo.InvariantCulture) +
                    clr.B.ToString("X2", CultureInfo.InvariantCulture);
            }
            else
            {
                return "#FFFFFFFF";
            }
        }
    }
}
