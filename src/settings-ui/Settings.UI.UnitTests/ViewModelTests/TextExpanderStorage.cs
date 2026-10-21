// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class TextExpanderStorage
    {
        private string _root;
        private string _local;
        private TestStorageEnvironment _environment;

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpander-Storage-" + Guid.NewGuid().ToString("N"));
            _local = Path.Combine(_root, "local");
            Directory.CreateDirectory(_local);
            _environment = new TestStorageEnvironment(_root, _local);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_root, recursive: true);
        }

        [TestMethod]
        public void EmptyFolderReceivesCurrentFileBeforeItBecomesActive()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Legacy, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsFalse(change.HasExistingFile);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            string target = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current);
            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual(targetFolder, model.SnippetsPath);
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(target));
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void UsingFolderFileBacksUpThePreviousActiveFile()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.UseFolder, out string backup));

            Assert.IsTrue(File.Exists(backup));
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(backup));
            Assert.AreEqual("!shared", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void ReplacingFolderFileBacksUpItsPreviousContents()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual("!shared=Shared", File.ReadAllText(backup));
            Assert.AreEqual("!local=Local", File.ReadAllText(target));
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void FileAppearingAfterFolderSelectionIsNotOverwritten()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsFalse(change.HasExistingFile);
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Shared");

            Assert.AreEqual(
                "TextExpander_SnippetFolder_FileAppeared",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));
            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual("!shared=Shared", File.ReadAllText(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        public void ClearingBacksUpThenEmptiesTheActiveFile()
        {
            string active = Write(_local, TextExpanderSnippetFileNames.Current, "!one=One\n!two=Two");
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.ClearSnippets(out string backup));

            Assert.AreEqual("!one=One\n!two=Two", File.ReadAllText(backup));
            Assert.AreEqual(0, new FileInfo(active).Length);
            Assert.AreEqual(0, model.Snippets.Count);
            Assert.IsFalse(model.CanClearSnippets);
        }

        private TextExpanderViewModel CreateViewModel()
        {
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            var utils = new Mock<SettingsUtils>(new FileSystem(), null);
            utils.Setup(u => u.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(settings);
            var repository = new Mock<ISettingsRepository<GeneralSettings>>();
            repository.SetupGet(r => r.SettingsConfig).Returns(new GeneralSettings());
            return new TextExpanderViewModel(
                utils.Object,
                repository.Object,
                _ => 0,
                null,
                isElevated: false,
                getResourceString: key => key,
                storageEnvironment: _environment);
        }

        private static string Write(string folder, string name, string content)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content);
            return path;
        }

        private sealed class TestStorageEnvironment : ITextExpanderStorageEnvironment
        {
            private readonly string _root;

            public TestStorageEnvironment(string root, string defaultSnippetsFolder)
            {
                _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                DefaultSnippetsFolder = defaultSnippetsFolder;
            }

            public string DefaultSnippetsFolder { get; }

            public string ExpandEnvironmentVariables(string value) => value;

            public bool FileExists(string path) => File.Exists(Guard(path));

            public void CreateDirectory(string path) => Directory.CreateDirectory(Guard(path));

            public byte[] ReadAllBytes(string path) => File.ReadAllBytes(Guard(path));

            public void WriteNewFile(string path, byte[] bytes)
            {
                using var stream = new FileStream(Guard(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes, 0, bytes.Length);
            }

            public void MoveFile(string source, string destination) => File.Move(Guard(source), Guard(destination), overwrite: false);

            public void ReplaceFile(string source, string destination, string backup)
                => File.Replace(Guard(source), Guard(destination), backup == null ? null : Guard(backup), ignoreMetadataErrors: true);

            public void DeleteFile(string path) => File.Delete(Guard(path));

            public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed) => EmptyDisposable.Instance;

            private string Guard(string path)
            {
                string full = Path.GetFullPath(path);
                if (!full.Equals(_root, StringComparison.OrdinalIgnoreCase)
                    && !full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail("The test touched a path outside its temporary folder: " + full);
                }

                return full;
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static EmptyDisposable Instance { get; } = new EmptyDisposable();

            public void Dispose()
            {
            }
        }
    }
}
