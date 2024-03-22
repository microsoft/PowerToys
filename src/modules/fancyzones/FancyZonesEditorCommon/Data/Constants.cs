// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FancyZonesEditorCommon.Data
{
    public static class Constants
    {
        public enum CustomLayout
        {
            Canvas,
            Grid,
        }

        public static readonly ReadOnlyDictionary<CustomLayout, string> CustomLayoutTypeJsonTags = new ReadOnlyDictionary<CustomLayout, string>(
            new Dictionary<CustomLayout, string>()
            {
                { CustomLayout.Canvas, "canvas" },
                { CustomLayout.Grid, "grid" },
            });
    }
}
