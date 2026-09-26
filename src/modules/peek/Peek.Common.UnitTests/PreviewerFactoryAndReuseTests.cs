// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Storage;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class PreviewerFactoryAndReuseTests
    {
        [TestMethod]
        public void PreviewerFactory_GetCompatiblePreviewerType_SelectsReusablePreviewerType()
        {
            var item = new TestFileSystemItem("sample.reuse");
            var factory = CreateTestFactory();

            var compatibleType = factory.GetCompatiblePreviewerType(item);

            Assert.AreEqual(typeof(ReusableTestPreviewer), compatibleType);
            Assert.IsTrue(typeof(IReusablePreviewer).IsAssignableFrom(compatibleType));
        }

        [TestMethod]
        public void PreviewerFactory_GetCompatiblePreviewerType_SelectsNonReusablePreviewerType()
        {
            var item = new TestFileSystemItem("sample.nonreuse");
            var factory = CreateTestFactory();

            var compatibleType = factory.GetCompatiblePreviewerType(item);

            Assert.AreEqual(typeof(NonReusableTestPreviewer), compatibleType);
            Assert.IsFalse(typeof(IReusablePreviewer).IsAssignableFrom(compatibleType));
        }

        [TestMethod]
        public void ReusablePreviewer_RebindUpdatesContext()
        {
            var originalItem = new TestFileSystemItem("first.reuse");
            var nextItem = new TestFileSystemItem("second.reuse");
            var previewer = new ReusableTestPreviewer(originalItem);

            previewer.Rebind(nextItem, 1.75);

            Assert.AreSame(nextItem, previewer.BoundItem);
            Assert.AreEqual(1.75, previewer.BoundScalingFactor, 0.0001);
        }

        private static PreviewerFactory CreateTestFactory()
        {
            var registrations = new[]
            {
                new PreviewerFactory.PreviewerDefinition(
                    typeof(ReusableTestPreviewer),
                    item => string.Equals(item.Extension, ".reuse", StringComparison.OrdinalIgnoreCase),
                    item => new ReusableTestPreviewer(item)),
                new PreviewerFactory.PreviewerDefinition(
                    typeof(NonReusableTestPreviewer),
                    item => string.Equals(item.Extension, ".nonreuse", StringComparison.OrdinalIgnoreCase),
                    item => new NonReusableTestPreviewer(item)),
                new PreviewerFactory.PreviewerDefinition(
                    typeof(NonReusableTestPreviewer),
                    _ => true,
                    item => new NonReusableTestPreviewer(item)),
            };

            return new PreviewerFactory(new TestPreviewSettings(), registrations);
        }

        private sealed class ReusableTestPreviewer : IPreviewer, IReusablePreviewer
        {
            public ReusableTestPreviewer(IFileSystemItem item)
            {
                BoundItem = item;
            }

            public event PropertyChangedEventHandler? PropertyChanged
            {
                add { }
                remove { }
            }

            public IFileSystemItem BoundItem { get; private set; }

            public double BoundScalingFactor { get; private set; }

            public PreviewState State { get; set; }

            public Task CopyAsync() => Task.CompletedTask;

            public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
                => Task.FromResult(new PreviewSize());

            public Task LoadPreviewAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public void Rebind(IFileSystemItem item, double scalingFactor)
            {
                BoundItem = item;
                BoundScalingFactor = scalingFactor;
            }
        }

        private sealed class NonReusableTestPreviewer : IPreviewer
        {
            public NonReusableTestPreviewer(IFileSystemItem item)
            {
                _ = item;
            }

            public event PropertyChangedEventHandler? PropertyChanged
            {
                add { }
                remove { }
            }

            public PreviewState State { get; set; }

            public Task CopyAsync() => Task.CompletedTask;

            public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
                => Task.FromResult(new PreviewSize());

            public Task LoadPreviewAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class TestPreviewSettings : IPreviewSettings
        {
            public bool SourceCodeWrapText => false;

            public bool SourceCodeTryFormat => false;

            public int SourceCodeFontSize => 12;

            public bool SourceCodeStickyScroll => false;

            public bool SourceCodeMinimap => false;
        }

        private sealed class TestFileSystemItem : IFileSystemItem
        {
            public TestFileSystemItem(string fileName)
            {
                Name = fileName;
                Extension = System.IO.Path.GetExtension(fileName);
                Path = $"C:\\temp\\{fileName}";
                ParsingName = Path;
            }

            public string Extension { get; }

            public string Name { get; }

            public string ParsingName { get; }

            public string Path { get; }

            public Task<IStorageItem?> GetStorageItemAsync() => Task.FromResult<IStorageItem?>(null);
        }
    }
}
