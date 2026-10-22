// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>A folder selected for the snippet library, inspected before any file is changed.</summary>
    public sealed class TextExpanderFolderChange
    {
        internal TextExpanderFolderChange(
            string folderPath,
            string sourceFilePath,
            TextExpanderFileIdentity sourceIdentity,
            TextExpanderFolderIdentity folderIdentity)
        {
            FolderPath = folderPath;
            SourceFilePath = sourceFilePath;
            SourceIdentity = sourceIdentity;
            FolderIdentity = folderIdentity;
            FilePath = folderIdentity.ResolveFilePath();
            HasExistingFile = folderIdentity.IdentityFor(FilePath).Exists;
        }

        public string FolderPath { get; }

        public string FilePath { get; }

        public bool HasExistingFile { get; }

        internal string SourceFilePath { get; }

        internal bool SourceFileExisted => SourceIdentity.Exists;

        internal TextExpanderFileIdentity SourceIdentity { get; }

        internal TextExpanderFolderIdentity FolderIdentity { get; }
    }
}
