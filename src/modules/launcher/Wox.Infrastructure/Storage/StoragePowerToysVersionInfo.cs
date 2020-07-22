using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Markup;

namespace Wox.Infrastructure.Storage
{
    public class StoragePowerToysVersionInfo
    {
        // This detail is accessed by the storage items and is used to decide if the cache must be deleted or not
        public bool clearCache = false;


        private String currentPowerToysVersion = String.Empty;
        private String FilePath { get; set; } = String.Empty;

        // As of now this information is not pertinent but may be in the future
        // There may be cases when we want to delete only the .cache files and not the .json storage files
        private enum StorageType
        {
            BINARY_STORAGE = 0,
            JSON_STORAGE = 1
        }

        // To compare the version numbers
        public static bool Lessthan(string version1, string version2)
        {
            string version = "v";
            string period = ".";
            const int versionLength = 3;

            // If there is some error in populating/retrieving the version numbers, then the cache must be deleted
            // This case will not be hit, but is present as a fail safe
            if (String.IsNullOrEmpty(version1) || String.IsNullOrEmpty(version2))
            {
                return true;
            }

            string[] split1 = version1.Split(new string[] { version, period }, StringSplitOptions.RemoveEmptyEntries);
            string[] split2 = version2.Split(new string[] { version, period }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < versionLength; i++)
            {
                if (int.Parse(split1[i]) < int.Parse(split2[i]))
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

        private string GetFilePath(String AssociatedFilePath, int type)
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

            string filePath = AssociatedFilePath.Substring(0, AssociatedFilePath.Length - suffix.Length) + "_version.txt";
            return filePath;
        }

        public StoragePowerToysVersionInfo(String AssociatedFilePath, int type)
        {
            FilePath = GetFilePath(AssociatedFilePath, type);
            // Get the previous version of PowerToys and cache Storage details from the CacheDetails.json storage file
            String previousVersion = GetPreviousVersion();
            currentPowerToysVersion = Microsoft.PowerToys.Settings.UI.Lib.Utilities.Helper.GetProductVersion();

            // If the previous version is below a set threshold, then we want to delete the file
            // However, we do not want to delete the cache if the same version of powerToys is being launched
            if (Lessthan(previousVersion, currentPowerToysVersion))
            {
                clearCache = true;
            }
        }

        public void Close()
        {
            // Update the Version file to the current version of powertoys
            File.WriteAllText(FilePath, currentPowerToysVersion);
        }
    }
}
