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
    public class SearchLocation
    {
        public string City { get; set; }

        public string Country { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public SearchLocation(string city, string country, double latitude, double longitude)
        {
            City = city;
            Country = country;
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
