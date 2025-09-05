// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualBasic.Logging;
using Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public static class SearchLocationLoader
    {
        /// <summary>
        /// Loads all cities from a world_cities.csv file.
        /// Expected columns: city, city_ascii, lat, lng, country
        /// </summary>
        public static IEnumerable<SearchLocation> LoadCities(string path)
        {
            using StreamReader reader = new(path);

            // Read header
            string header = reader.ReadLine();
            if (header is null)
            {
                yield break;
            }

            List<string> headerCols = SplitCsvLine(header);
            int idxCity = headerCols.FindIndex(h => string.Equals(h, "city", StringComparison.OrdinalIgnoreCase));
            int idxCityAscii = headerCols.FindIndex(h => string.Equals(h, "city_ascii", StringComparison.OrdinalIgnoreCase));
            int idxLat = headerCols.FindIndex(h => string.Equals(h, "lat", StringComparison.OrdinalIgnoreCase));
            int idxLng = headerCols.FindIndex(h => string.Equals(h, "lng", StringComparison.OrdinalIgnoreCase));
            int idxCountry = headerCols.FindIndex(h => string.Equals(h, "country", StringComparison.OrdinalIgnoreCase));

            string line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> cols = SplitCsvLine(line);

                string rawCity = idxCity >= 0 && idxCity < cols.Count ? cols[idxCity] : string.Empty;
                string cityAscii = idxCityAscii >= 0 && idxCityAscii < cols.Count ? cols[idxCityAscii] : string.Empty;
                string cityName = string.IsNullOrWhiteSpace(cityAscii) ? rawCity : cityAscii;
                string country = idxCountry >= 0 && idxCountry < cols.Count ? cols[idxCountry] : string.Empty;

                if (!(idxLat >= 0 && idxLat < cols.Count && idxLng >= 0 && idxLng < cols.Count))
                {
                    continue;
                }

                if (!double.TryParse(cols[idxLat], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat))
                {
                    continue;
                }

                if (!double.TryParse(cols[idxLng], NumberStyles.Float, CultureInfo.InvariantCulture, out double lng))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cityName))
                {
                    continue;
                }

                yield return new SearchLocation() { City = cityName, Country = country, Latitude = lat, Longitude = lng };
            }
        }

        /// <summary>
        /// Splits a CSV line, handling commas inside quotes and escaped quotes.
        /// </summary>
        private static List<string> SplitCsvLine(string line)
        {
            List<string> result = new();
            if (string.IsNullOrEmpty(line))
            {
                result.Add(string.Empty);
                return result;
            }

            bool inQuotes = false;
            StringBuilder current = new();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result;
        }
    }
}
