// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Peek.Common.Models;
using Peek.FilePreviewer.Previewers;
using Peek.FilePreviewer.Previewers.Archives;

using Windows.Storage;

namespace Peek.FilePreviewer.UnitTests;

[TestClass]
public class PreviewerSupportTests
{
    [TestMethod]
    public void IsItemSupported_RoutesFileTypesToExpectedPreviewers()
    {
        Assert.IsTrue(WebBrowserPreviewer.IsItemSupported(new TestFileSystemItem(".md")));
        Assert.IsTrue(WebBrowserPreviewer.IsItemSupported(new TestFileSystemItem(".svg")));
        Assert.IsTrue(WebBrowserPreviewer.IsItemSupported(new TestFileSystemItem(".pdf")));

        Assert.IsTrue(ArchivePreviewer.IsItemSupported(new TestFileSystemItem(".zip")));
        Assert.IsTrue(ArchivePreviewer.IsItemSupported(new TestFileSystemItem(".tgz")));

        Assert.IsFalse(ArchivePreviewer.IsItemSupported(new TestFileSystemItem(".txt")));
    }

    private sealed class TestFileSystemItem : IFileSystemItem
    {
        public TestFileSystemItem(string extension)
        {
            Extension = extension;
        }

        public string Extension { get; }

        public string Name => $"test{Extension}";

        public string ParsingName => Name;

        public string Path => Name;

        public Task<IStorageItem?> GetStorageItemAsync() => Task.FromResult<IStorageItem?>(null);
    }
}
