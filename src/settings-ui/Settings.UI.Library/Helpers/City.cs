// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Settings.UI.Library.Helpers
{
    public sealed record City(string Name, string Country, double Latitude, double Longitude)
    {
        public string Display => string.IsNullOrWhiteSpace(Country)
            ? Name
            : $"{Name}, {Country}";
    }
}
