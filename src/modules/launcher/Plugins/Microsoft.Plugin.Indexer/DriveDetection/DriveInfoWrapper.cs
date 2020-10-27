// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Plugin.Indexer.Interface;

namespace Microsoft.Plugin.Indexer.DriveDetection
{
    public class DriveInfoWrapper : IDriveInfoWrapper
    {
        private static readonly int DriveCount = GetDriveInfo();

        private static int GetDriveInfo()
        {
            return DriveInfo.GetDrives().Length;
        }

        public int GetDriveCount() => DriveCount;
    }
}
