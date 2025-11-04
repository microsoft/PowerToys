// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;

using ImageResizer.Properties;

namespace ImageResizer.Helpers
{
    internal static class AiSuperResolutionFormatter
    {
        private static readonly CompositeFormat ScaleFormat = CompositeFormat.Parse(Resources.Input_AiScaleFormat);

        public static string FormatScaleName(int scale)
        {
            return string.Format(CultureInfo.CurrentCulture, ScaleFormat, scale);
        }
    }
}
