// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;

namespace Microsoft.Plugin.Indexer.DriveDetection
{
    public class DriveInfoWrapper : IDriveInfoWrapper
    {
        private static readonly int DriveCount = GetDriveInfo();

        private static int GetDriveInfo()
        {
            // To ignore removable type drives, CD ROMS, no root partitions which may not be formatted and only return the fixed drives in the system.
            return DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Count();
        }

        public int GetDriveCount() => DriveCount;
    }
}
