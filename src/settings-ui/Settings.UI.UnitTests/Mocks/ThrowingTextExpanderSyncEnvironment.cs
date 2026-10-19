// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    /// <summary>Fails the test on any use, proving elevated Settings never reaches OneDrive or the file system.</summary>
    internal sealed class ThrowingTextExpanderSyncEnvironment : ITextExpanderSyncEnvironment
    {
        public string MachineName => throw Touched();

        public string DefaultSnippetsFolder => throw Touched();

        public string GetEnvironmentVariable(string name) => throw Touched();

        public string ExpandEnvironmentVariables(string value) => throw Touched();

        public bool DirectoryExists(string path) => throw Touched();

        public bool FileExists(string path) => throw Touched();

        public void CreateDirectory(string path) => throw Touched();

        public byte[] ReadAllBytes(string path) => throw Touched();

        public void WriteNewFile(string path, byte[] bytes) => throw Touched();

        public void MoveFile(string source, string destination) => throw Touched();

        public void ReplaceFile(string source, string destination, string backup) => throw Touched();

        public void DeleteFile(string path) => throw Touched();

        public DateTime GetLastWriteTime(string path) => throw Touched();

        public FileAttributes GetAttributes(string path) => throw Touched();

        public void SetAttributes(string path, FileAttributes attributes) => throw Touched();

        public string FindOneDriveExecutable() => throw Touched();

        public void StartProcess(string path) => throw Touched();

        public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed) => throw Touched();

        public IReadOnlyList<TextExpanderFolderEntry> ListFolder(string folder, IReadOnlyList<string> fileNames) => throw Touched();

        private static AssertFailedException Touched([CallerMemberName] string member = null)
            => new AssertFailedException("Elevated Settings used " + member + ".");
    }
}
