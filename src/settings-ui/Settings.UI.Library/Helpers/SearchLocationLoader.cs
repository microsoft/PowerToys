// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public static class SearchLocationLoader
    {
        private static readonly List<SearchLocation> LocationDataList = new List<SearchLocation>();

        public static IEnumerable<SearchLocation> GetAll()
        {
            return LocationDataList
                .GroupBy(l => $"{l.Country}|{l.City}|{l.Latitude.ToString(CultureInfo.InvariantCulture)}|{l.Longitude.ToString(CultureInfo.InvariantCulture)}")
                .Select(g => g.First())
                .OrderBy(l => l.Country, StringComparer.OrdinalIgnoreCase)
                .ThenBy(l => l.City, StringComparer.OrdinalIgnoreCase);
        }
    }
}
