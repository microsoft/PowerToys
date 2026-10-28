// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Storage;

namespace Microsoft.PowerToys.TextExpander.Editor.ViewModels
{
    internal sealed class TextExpanderFolderIdentity
    {
        public TextExpanderFolderIdentity(string folderPath, TextExpanderFileIdentity current, TextExpanderFileIdentity legacy)
        {
            FolderPath = folderPath;
            Current = current;
            Legacy = legacy;
        }

        public string FolderPath { get; }

        public string CurrentPath => Path.Combine(FolderPath, TextExpanderSnippetFileNames.Current);

        public string LegacyPath => Path.Combine(FolderPath, TextExpanderSnippetFileNames.Legacy);

        public TextExpanderFileIdentity Current { get; }

        public TextExpanderFileIdentity Legacy { get; }

        public string ResolveFilePath()
        {
            if (Current.Exists)
            {
                return Legacy.Exists && TextExpanderStarter.IsUntouchedStarterBytes(Current.Bytes)
                    ? LegacyPath
                    : CurrentPath;
            }

            return Legacy.Exists ? LegacyPath : CurrentPath;
        }

        public TextExpanderFileIdentity IdentityFor(string path)
            => string.Equals(path, LegacyPath, StringComparison.OrdinalIgnoreCase) ? Legacy : Current;

        public TextExpanderFolderIdentity WithFile(string path, TextExpanderFileIdentity identity)
            => string.Equals(path, LegacyPath, StringComparison.OrdinalIgnoreCase)
                ? new TextExpanderFolderIdentity(FolderPath, Current, identity)
                : new TextExpanderFolderIdentity(FolderPath, identity, Legacy);

        public bool SameAs(TextExpanderFolderIdentity other)
            => other is not null
                && string.Equals(FolderPath, other.FolderPath, StringComparison.OrdinalIgnoreCase)
                && Current.SameAs(other.Current)
                && Legacy.SameAs(other.Legacy);
    }
}
