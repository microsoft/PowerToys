// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;

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

        private class StorageObject
        {
            public string Version { get; set; }

            public string DefaultContent { get; set; }
        }

        // To compare the version numbers
        public static bool Lessthan(string version1, string version2)
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
                if (Path.GetExtension(FilePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    // Read and deserialize the JSON file
                    string json = File.ReadAllText(FilePath);
                    var versionObject = JsonSerializer.Deserialize<StorageObject>(json);
                    return versionObject?.Version ?? "v0.0.0"; // Returns "v0.0.0" if version is null
                }
                else if (Path.GetExtension(FilePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    // Read the txt file content directly
                    return File.ReadAllText(FilePath);
                }
            }

            // If the file doesn't exist or is not a recognized format, assume an old version
            return "v0.0.0";
        }

        public string GetFilePath(string associatedFilePath, int type)
        {
            string suffix = string.Empty;
            string fileType = string.Empty;

            string cacheSuffix = ".cache";
            string jsonSuffix = ".json";

            string cachceFileType = "_version.txt";
            string jsonFileType = "_information.json";

            if (type == (uint)StorageType.BINARY_STORAGE)
            {
                suffix = cacheSuffix;
                fileType = cachceFileType;
            }
            else if (type == (uint)StorageType.JSON_STORAGE)
            {
                suffix = jsonSuffix;
                fileType = jsonFileType;
            }

            string filePath = string.Concat(associatedFilePath.AsSpan(0, associatedFilePath.Length - suffix.Length), fileType);
            return filePath;
        }

        public StoragePowerToysVersionInfo(string associatedFilePath, int type)
        {
            if (associatedFilePath == null)
            {
                throw new ArgumentNullException(nameof(associatedFilePath));
            }

            FilePath = GetFilePath(associatedFilePath, type);

            // Get the previous version of PowerToys and cache Storage details from the CacheDetails.json storage file
            string previousVersion = GetPreviousVersion();
            currentPowerToysVersion = Microsoft.PowerToys.Settings.UI.Library.Utilities.Helper.GetProductVersion();

            // If the previous version is below a set threshold, then we want to delete the file
            // However, we do not want to delete the cache if the same version of powerToys is being launched
            if (Lessthan(previousVersion, currentPowerToysVersion))
            {
                ClearCache = true;
            }
        }

        public void Close(string defaultContent)
        {
            if (Path.GetExtension(FilePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                // Create an object that includes both the current version and default content
                var dataToSerialize = new StorageObject
                {
                    Version = currentPowerToysVersion,
                    DefaultContent = defaultContent,
                };

                // Serialize the StorageObject to a JSON string
                string json = JsonSerializer.Serialize(dataToSerialize, new JsonSerializerOptions { WriteIndented = true });

                // Write the JSON string to the file
                File.WriteAllText(FilePath, json);
            }
            else if (Path.GetExtension(FilePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                // For a txt file, just write the version as plain text
                File.WriteAllText(FilePath, currentPowerToysVersion);
            }
        }
    }
}
