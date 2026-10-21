// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>A folder selected for the snippet library, inspected before any file is changed.</summary>
    public sealed class TextExpanderFolderChange
    {
        internal TextExpanderFolderChange(string folderPath, string filePath, bool hasExistingFile)
        {
            FolderPath = folderPath;
            FilePath = filePath;
            HasExistingFile = hasExistingFile;
        }

        public string FolderPath { get; }

        public string FilePath { get; }

        public bool HasExistingFile { get; }
    }
}
