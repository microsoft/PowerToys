using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Markup;

namespace Wox.Infrastructure.Storage
{
    public class StorageHelper
    {
        cacheDetails _details { get; set; }

        public bool clearCache = false;

        private enum StorageType
        {
            BINARY_STORAGE = 0,
            JSON_STORAGE = 1
        }
        public class cacheDetails
        {
            public string previousVersionNumber = String.Empty;
        }

        public static bool Lessthan(string version1, string version2)
        {
            string[] split1 = version1.Split( new string[] { "v", "." }, StringSplitOptions.RemoveEmptyEntries); 
            string[] split2 = version2.Split( new string[] { "v", "." }, StringSplitOptions.RemoveEmptyEntries); 

            for(int i=0; i<3; i++)
            {
                if(int.Parse(split1[i]) < int.Parse(split2[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public string GetPreviousVersion()
        {
            const string directoryName = "Cache";
            var directoryPath = Path.Combine(Constant.DataDirectory, directoryName);
            Helper.ValidateDirectory(directoryPath);
            var filename = "previousVersion";
            const string fileSuffix = ".txt";
            var FilePath = Path.Combine(directoryPath, $"{filename}{fileSuffix}");
            if(File.Exists(FilePath))
            {
                return File.ReadAllText(FilePath);
            }
            else
            {
                // which means it's an old version
                File.WriteAllText(FilePath, "v0.0.0");
                return "v0.0.0";
            }
        }

        private String currentPowerToysVersion = String.Empty;

        public StorageHelper(int type)
        {
            // Get the previous version of PowerToys and cache Storage details from the CacheDetails.json storage file
            String previousVersion = GetPreviousVersion();
            currentPowerToysVersion = Microsoft.PowerToys.Settings.UI.Lib.Utilities.Helper.GetProductVersion();
            String portableVersion = "v1.0.0";

            // If the previous version is below a set threshold, then we want to delete the file
            // However, we do not want to delete the cache if the same version of powerToys is being launched
            if (Lessthan(previousVersion, portableVersion) && previousVersion != currentPowerToysVersion)
            {
                clearCache = true;
            }

            // If it is of type binary storage, then we want to clean up the cache to make sure that any changes made in the class type do not affect the loading of the cache
            if (type == (uint)StorageType.BINARY_STORAGE)
            {
                clearCache = true;
            }

        }

        public void Close()
        {
            const string directoryName = "Cache";
            var directoryPath = Path.Combine(Constant.DataDirectory, directoryName);
            Helper.ValidateDirectory(directoryPath);
            var filename = "previousVersion";
            const string fileSuffix = ".txt";
            var FilePath = Path.Combine(directoryPath, $"{filename}{fileSuffix}");
                // which means it's an old version
            File.WriteAllText(FilePath, currentPowerToysVersion);
        }
    }
}
