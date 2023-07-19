// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;

namespace Wox.Plugin
{
    public static class Constant
    {
        /// <summary>
        /// http://www.yinwang.org/blog-cn/2015/11/21/programming-philosophy
        /// </summary>
        public static string NonNull(this string obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            else
            {
                return obj;
            }
        }

        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IDirectory Directory = FileSystem.Directory;

        public const string ExeFileName = "PowerToys.PowerLauncher";
        public const string ModuleLocation = "Microsoft\\PowerToys\\PowerToys Run";
        public const string Plugins = "RunPlugins";
        public const string PluginsDataStorage = "Plugins";
        public const string PortableFolderName = "UserData";

        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        public static readonly string ProgramDirectory = Directory.GetParent(Assembly.Location.NonNull()).ToString();
        public static readonly string ExecutablePath = Path.Combine(ProgramDirectory, ExeFileName + ".exe");

        public static bool IsPortableMode { get; set; }

        public static string PortableDataPath { get; set; } = Path.Combine(ProgramDirectory, PortableFolderName);

        public static string DetermineDataDirectory()
        {
            if (Directory.Exists(PortableDataPath))
            {
                IsPortableMode = true;
                return PortableDataPath;
            }
            else
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ModuleLocation);
            }
        }

        public static readonly string DataDirectory = DetermineDataDirectory();
        public static readonly string PluginsDirectory = Path.Combine(DataDirectory, PluginsDataStorage);
        public static readonly string PreinstalledDirectory = Path.Combine(ProgramDirectory, Plugins);
        public const string Issue = "https://aka.ms/powerToysReportBug";
        public static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location.NonNull()).ProductVersion;

        public static readonly int ThumbnailSize = 64;
        public static readonly string ErrorIcon = Path.Combine(ProgramDirectory, "Assets", "PowerLauncher", "app_error.dark.png");
        public static readonly string LightThemedErrorIcon = Path.Combine(ProgramDirectory, "Assets", "PowerLauncher", "app_error.light.png");
    }
}
