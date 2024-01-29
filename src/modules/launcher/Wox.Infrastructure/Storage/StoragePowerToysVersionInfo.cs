// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Storage
{
    public class StoragePowerToysVersionInfo
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IFile File = FileSystem.File;

        // This detail is accessed by the storage items and is used to decide if the cache must be deleted or not
        public bool ClearCache { get; set; }

        private readonly string currentPowerToysVersion = string.Empty;

        private string FilePath { get; set; } = string.Empty;

        // As of now this information is not pertinent but may be in the future
        // There may be cases when we want to delete only the .cache files and not the .json storage files
        private enum StorageType
        {
            BINARY_STORAGE = 0,
            JSON_STORAGE = 1,
        }

        // To compare the version numbers
        public static bool LessThan(string version1, string version2)
        {
            string version = "v";
            string period = ".";
            const int versionLength = 3;

            // If there is some error in populating/retrieving the version numbers, then the cache must be deleted
            // This case will not be hit, but is present as a fail safe
            if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2))
            {
                return true;
            }

            string[] split1 = version1.Split(new string[] { version, period }, StringSplitOptions.RemoveEmptyEntries);
            string[] split2 = version2.Split(new string[] { version, period }, StringSplitOptions.RemoveEmptyEntries);

            // If an incomplete file write resulted in the version number not being saved completely, then the cache must be deleted
            if (split1.Length != split2.Length || split1.Length != versionLength)
            {
                return true;
            }

            for (int i = 0; i < versionLength; i++)
            {
                if (int.TryParse(split1[i], out int version1AsInt) && int.TryParse(split2[i], out int version2AsInt))
                {
                    if (version1AsInt < version2AsInt)
                    {
                        return true;
                    }
                }

                // If either of the values could not be parsed, the version number was not saved correctly and the cache must be deleted
                else
                {
                    return true;
                }
            }

            return false;
        }

        public string GetPreviousVersion()
        {
            if (File.Exists(FilePath))
            {
                return File.ReadAllText(FilePath);
            }
            else
            {
                // which means it's an old version of PowerToys
                string oldVersion = "v0.0.0";
                return oldVersion;
            }
        }

        private static string GetFilePath(string associatedFilePath, int type)
        {
            string suffix = string.Empty;
            string cacheSuffix = ".cache";
            string jsonSuffix = ".json";

            if (type == (uint)StorageType.BINARY_STORAGE)
            {
                suffix = cacheSuffix;
            }
            else if (type == (uint)StorageType.JSON_STORAGE)
            {
                suffix = jsonSuffix;
            }

            string filePath = string.Concat(associatedFilePath.AsSpan(0, associatedFilePath.Length - suffix.Length), "_version.txt");
            return filePath;
        }

        public StoragePowerToysVersionInfo(string associatedFilePath, int type)
        {
            ArgumentNullException.ThrowIfNull(associatedFilePath);

            FilePath = GetFilePath(associatedFilePath, type);

            // Get the previous version of PowerToys and cache Storage details from the CacheDetails.json storage file
            string previousVersion = GetPreviousVersion();
            currentPowerToysVersion = Microsoft.PowerToys.Settings.UI.Library.Utilities.Helper.GetProductVersion();

            // If the previous version is below a set threshold, then we want to delete the file
            // However, we do not want to delete the cache if the same version of powerToys is being launched
            if (LessThan(previousVersion, currentPowerToysVersion))
            {
                ClearCache = true;
            }
        }

        public void Close()
        {
            try
            {
                // Update the Version file to the current version of powertoys
                File.WriteAllText(FilePath, currentPowerToysVersion);
            }
            catch (System.Exception e)
            {
                Log.Exception($"Error in saving version at <{FilePath}>", e, GetType());
            }
        }
    }
}
