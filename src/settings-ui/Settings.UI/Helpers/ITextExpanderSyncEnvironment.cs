// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Everything the Text Expander OneDrive setup reads or changes outside its view model, so
    /// tests can use temporary folders instead of a real OneDrive.
    /// </summary>
    public interface ITextExpanderSyncEnvironment
    {
        string MachineName { get; }

        /// <summary>Gets the folder Text Expander uses when no snippet folder is set.</summary>
        string DefaultSnippetsFolder { get; }

        string GetEnvironmentVariable(string name);

        string ExpandEnvironmentVariables(string value);

        bool DirectoryExists(string path);

        bool FileExists(string path);

        void CreateDirectory(string path);

        byte[] ReadAllBytes(string path);

        /// <summary>Creates <paramref name="path"/>, which must not exist yet, and flushes it to disk.</summary>
        void WriteNewFile(string path, byte[] bytes);

        /// <summary>Moves a file. Fails if <paramref name="destination"/> exists.</summary>
        void MoveFile(string source, string destination);

        /// <summary>Replaces <paramref name="destination"/> with <paramref name="source"/>, keeping the old file as <paramref name="backup"/>.</summary>
        void ReplaceFile(string source, string destination, string backup);

        void DeleteFile(string path);

        DateTime GetLastWriteTime(string path);

        FileAttributes GetAttributes(string path);

        void SetAttributes(string path, FileAttributes attributes);

        /// <summary>Returns the full path of the installed OneDrive app, or null.</summary>
        string FindOneDriveExecutable();

        void StartProcess(string path);

        /// <summary>
        /// Calls <paramref name="changed"/>, on any thread, whenever one of <paramref name="fileNames"/>
        /// may have appeared in <paramref name="folder"/>. The folder does not need to exist yet.
        /// </summary>
        IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed);

        /// <summary>
        /// Lists the subfolders of <paramref name="folder"/>, and the files in it named one of
        /// <paramref name="fileNames"/>, from the folder's own listing: names, attributes and times,
        /// never file content. Empty if the folder can't be listed. Safe on any thread.
        /// </summary>
        IReadOnlyList<TextExpanderFolderEntry> ListFolder(string folder, IReadOnlyList<string> fileNames);
    }
}
