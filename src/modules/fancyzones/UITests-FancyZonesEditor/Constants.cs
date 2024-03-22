// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.FancyZonesEditor.UITests
{
    public static class Constants
    {
        public enum CustomLayoutType
        {
            Canvas,
            Grid,
        }

        public static readonly Dictionary<CustomLayoutType, string> CustomLayoutTypeNames = new Dictionary<CustomLayoutType, string>()
        {
            { CustomLayoutType.Canvas, "canvas" },
            { CustomLayoutType.Grid, "grid" },
        };
    }
}
