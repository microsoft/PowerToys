// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Properties;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to help with the area path.
    /// </summary>
    internal static class AreaPathHelper
    {
        /// <summary>
        /// Return a string with the area path of a <see cref="WindowsSetting"/>. The result includes the delimiter sign.
        /// </summary>
        /// <param name="areaList"> The list the ordered areas to combine.
        internal static string CreateAreaPathString(in IList<string>? areaList)
        {
            if (areaList is null)
            {
                return string.Empty;
            }

            return string.Join(" \u25B9 ", areaList);
        }
    }
}
