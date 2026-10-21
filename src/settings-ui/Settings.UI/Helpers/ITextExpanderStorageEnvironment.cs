// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>File-system operations used to manage the active Text Expander snippet file.</summary>
    public interface ITextExpanderStorageEnvironment
    {
        string DefaultSnippetsFolder { get; }

        string ExpandEnvironmentVariables(string value);

        bool FileExists(string path);

        void CreateDirectory(string path);

        byte[] ReadAllBytes(string path);

        void WriteNewFile(string path, byte[] bytes);

        void MoveFile(string source, string destination);

        void ReplaceFile(string source, string destination, string backup);

        void DeleteFile(string path);

        IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed);
    }
}
