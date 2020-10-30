// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Microsoft.Plugin.Folder.Sources
{
    internal class DriveInformation : IDriveInformation
    {
        private static readonly List<string> DriverNames = InitialDriverList().ToList();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Do not want to change the behavior of the application, but want to enforce static analysis")]
        private static IEnumerable<string> InitialDriverList()
        {
            var directorySeparatorChar = System.IO.Path.DirectorySeparatorChar;

            // Using InvariantCulture since this is internal
            return DriveInfo.GetDrives()
                .Select(driver => driver.Name.ToLower(CultureInfo.InvariantCulture).TrimEnd(directorySeparatorChar));
        }

        public IEnumerable<string> GetDriveNames() => DriverNames;
    }
}
