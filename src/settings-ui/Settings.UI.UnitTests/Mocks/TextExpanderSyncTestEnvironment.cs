// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    /// <summary>
    /// A OneDrive setup confined to a temporary folder. Any path outside that folder fails the
    /// test, and only the test's own variables are expanded, so a real OneDrive is never touched.
    /// </summary>
    internal sealed class TextExpanderSyncTestEnvironment : ITextExpanderSyncEnvironment
    {
        private readonly string _root;

        public TextExpanderSyncTestEnvironment(string root, string defaultSnippetsFolder)
        {
            _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            DefaultSnippetsFolder = defaultSnippetsFolder;
        }

        public Dictionary<string, string> Variables { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, FileAttributes> AttributeOverrides { get; } = new Dictionary<string, FileAttributes>(StringComparer.OrdinalIgnoreCase);

        public List<FileAttributes> SetAttributeCalls { get; } = new List<FileAttributes>();

        public List<FakeFileWatch> Watches { get; } = new List<FakeFileWatch>();

        /// <summary>Gets every folder listed, in order.</summary>
        public List<string> Listed { get; } = new List<string>();

        public List<string> Started { get; } = new List<string>();

        public string OneDriveExecutable { get; set; }

        /// <summary>Gets the number of operations that changed the file system.</summary>
        public int Writes { get; private set; }

        /// <summary>Gets every file whose content was read, which for a OneDrive file would download it.</summary>
        public List<string> Reads { get; } = new List<string>();

        public string MachineName => "TESTPC";

        public string DefaultSnippetsFolder { get; }

        public string GetEnvironmentVariable(string name) => Variables.TryGetValue(name, out string value) ? value : null;

        public string ExpandEnvironmentVariables(string value)
        {
            foreach (KeyValuePair<string, string> variable in Variables)
            {
                value = value.Replace("%" + variable.Key + "%", variable.Value, StringComparison.OrdinalIgnoreCase);
            }

            return value;
        }

        public bool DirectoryExists(string path) => Directory.Exists(Guard(path));

        public bool FileExists(string path) => File.Exists(Guard(path));

        public void CreateDirectory(string path)
        {
            Writes++;
            Directory.CreateDirectory(Guard(path));
        }

        public byte[] ReadAllBytes(string path)
        {
            Reads.Add(Guard(path));
            return File.ReadAllBytes(Guard(path));
        }

        public void WriteNewFile(string path, byte[] bytes)
        {
            Writes++;
            using var stream = new FileStream(Guard(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
        }

        public void MoveFile(string source, string destination)
        {
            Writes++;
            File.Move(Guard(source), Guard(destination), overwrite: false);
        }

        public void ReplaceFile(string source, string destination, string backup)
        {
            Writes++;
            File.Replace(Guard(source), Guard(destination), backup == null ? null : Guard(backup), ignoreMetadataErrors: true);
        }

        public void DeleteFile(string path)
        {
            Writes++;
            File.Delete(Guard(path));
        }

        public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(Guard(path));

        public FileAttributes GetAttributes(string path)
            => AttributeOverrides.TryGetValue(Guard(path), out FileAttributes attributes) ? attributes : File.GetAttributes(path);

        public void SetAttributes(string path, FileAttributes attributes)
        {
            Writes++;
            SetAttributeCalls.Add(attributes);

            // A cloud file stays a placeholder until the sync provider downloads it.
            FileAttributes providerOwned = GetAttributes(path) & (FileAttributes.ReparsePoint | TextExpanderSyncViewModel.RecallOnDataAccessAttribute);
            AttributeOverrides[Guard(path)] = attributes | providerOwned;
        }

        public string FindOneDriveExecutable() => OneDriveExecutable;

        public void StartProcess(string path) => Started.Add(path);

        public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed)
        {
            var watch = new FakeFileWatch(Guard(folder), fileNames, changed);
            Watches.Add(watch);
            return watch;
        }

        public IReadOnlyList<TextExpanderFolderEntry> ListFolder(string folder, IReadOnlyList<string> fileNames)
        {
            Listed.Add(Guard(folder));
            return TextExpanderSyncEnvironment.List(Guard(folder), fileNames);
        }

        private string Guard(string path)
        {
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new AssertFailedException("The test touched a path outside its temporary folder: " + full);
            }

            return full;
        }
    }
}
