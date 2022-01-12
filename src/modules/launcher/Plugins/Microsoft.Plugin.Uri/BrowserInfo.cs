// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Text;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Uri
{
    internal class BrowserInfo
    {
        public const string MSEdgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        public const string MSEdgeName = "Microsoft Edge";

        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath FilePath = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;

        private static readonly RegistryWrapper _registryWrapper = new RegistryWrapper();

        public string Path { get; private set; }

        public string IconPath { get; private set; }

        public bool IsDefaultBrowserSet { get => !string.IsNullOrEmpty(Path); }

        public BrowserInfo(ManagedCommon.Theme theme)
        {
            Update(theme);
        }

        /// <param name="defaultToEdgeOnFail">If true, If this function fails, for any reason, the browser will be set to Microsoft Edge.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "We want to keep the process alive but will log the exception")]
        public void Update(ManagedCommon.Theme newTheme, bool defaultToEdgeOnFail = false)
        {
            try
            {
                var progId = _registryWrapper.GetRegistryValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice",
                    "ProgId");
                var programLocation =

                    // Resolve App Icon (UWP)
                    _registryWrapper.GetRegistryValue(
                        $@"HKEY_CLASSES_ROOT\{progId}\Application",
                        "ApplicationIcon")

                    // Resolves default  file association icon (UWP + Normal)
                    ?? _registryWrapper.GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}\DefaultIcon", null);

                // "Handles 'Indirect Strings' (UWP programs)"
                // Using Ordinal since this is internal and used with a symbol
                if (programLocation.StartsWith("@", StringComparison.Ordinal))
                {
                    var directProgramLocationStringBuilder = new StringBuilder(128);
                    if (NativeMethods.SHLoadIndirectString(
                            programLocation,
                            directProgramLocationStringBuilder,
                            (uint)directProgramLocationStringBuilder.Capacity,
                            IntPtr.Zero) ==
                        NativeMethods.Hresult.Ok)
                    {
                        // Check if there's a postfix with contract-white/contrast-black icon is available and use that instead
                        var directProgramLocation = directProgramLocationStringBuilder.ToString();
                        var themeIcon = newTheme == ManagedCommon.Theme.Light || newTheme == ManagedCommon.Theme.HighContrastWhite
                            ? "contrast-white"
                            : "contrast-black";
                        var extension = FilePath.GetExtension(directProgramLocation);
                        var themedProgLocation =
                            $"{directProgramLocation.Substring(0, directProgramLocation.Length - extension.Length)}_{themeIcon}{extension}";
                        IconPath = File.Exists(themedProgLocation)
                            ? themedProgLocation
                            : directProgramLocation;

                        Path = string.Equals(FilePath.GetExtension(directProgramLocation), ".exe", StringComparison.Ordinal)
                            ? directProgramLocation
                            : null;
                    }

                    throw new Exception("Could not load indirect string.");
                }
                else
                {
                    // Using Ordinal since this is internal and used with a symbol
                    var indexOfComma = programLocation.IndexOf(',', StringComparison.Ordinal);
                    IconPath = indexOfComma > 0
                        ? programLocation.Substring(0, indexOfComma)
                        : programLocation;
                    Path = IconPath;
                }

                if (string.IsNullOrEmpty(Path))
                {
                    throw new Exception("Browser path is null or empty.");
                }
            }
            catch (Exception e)
            {
                if (defaultToEdgeOnFail)
                {
                    Path = MSEdgePath;
                    IconPath = MSEdgePath;
                }
                else
                {
                    Path = null;
                    IconPath = null;
                }

                Log.Exception("Exception when retrieving browser path/name" + (defaultToEdgeOnFail ? "; Browser set to microsoft edge" : null), e, GetType());
            }
        }
    }
}
