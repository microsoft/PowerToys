// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.TextExpander.Storage;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>File-system operations used to manage the active Text Expander snippet file.</summary>
    public interface ITextExpanderStorageEnvironment
    {
        string DefaultSnippetsFolder { get; }

        string ExpandEnvironmentVariables(string value);

        bool FileExists(string path);

        void CreateDirectory(string path);

        void WriteNewFile(string path, byte[] bytes);

        void MoveFile(string source, string destination);

        void ReplaceFile(string source, string destination, string backup);

        HandleBoundFileDeletionResult DeleteOwnedFile(string path, byte[] expectedBytes);

        IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed);
    }
}
