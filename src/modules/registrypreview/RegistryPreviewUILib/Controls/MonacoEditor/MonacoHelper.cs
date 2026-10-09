// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;

namespace RegistryPreviewUILib
{
    public static class MonacoHelper
    {
        /// <summary>
        /// Name of the virtual host
        /// </summary>
        public const string VirtualHostName = "PowerToysLocalMonaco";

        public static string TempFolderPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\LocalLow\Microsoft\PowerToys\RegistryPreview-Temp");

        private static string _monacoDirectory;

        public static string GetRuntimeMonacoDirectory()
        {
            string codeBase = Assembly.GetExecutingAssembly().Location;
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(codeBase) ?? string.Empty, "Assets", "Monaco"));
            if (Path.Exists(path))
            {
                return path;
            }
            else
            {
                // We're likely in WinUI3Apps directory and need to go back to the base directory.
                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(codeBase) ?? string.Empty, "..", "Assets", "Monaco"));
            }
        }

        public static string MonacoDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_monacoDirectory))
                {
                    _monacoDirectory = GetRuntimeMonacoDirectory();
                }

                return _monacoDirectory;
            }
        }
    }
}
