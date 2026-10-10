// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers;
using Windows.Foundation;
using Windows.Storage;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class ReusablePreviewerTests
    {
        // Test stubs and seam subclasses.
        private sealed class MockFileSystemItem : IFileSystemItem
        {
            public string Path { get; set; } = @"C:\test\sample.jpg";

            public string Name { get; set; } = "sample.jpg";

            public string Extension { get; set; } = ".jpg";

            public string ParsingName { get; set; } = @"C:\test\sample.jpg";

            public DateTime? DateModified { get; set; } = DateTime.Now;

            public ulong FileSizeBytes { get; set; } = 1024;

            public string FileType { get; set; } = "JPEG Image";

            public Task<IStorageItem?> GetStorageItemAsync() => Task.FromResult<IStorageItem?>(null);
        }

        /// <summary>
        /// Create a non-null ImageSource in memory for testing without invoking WinUI COM
        /// activation.
        /// </summary>
        private static ImageSource CreateDummyImageSource() =>
            (ImageSource)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));

        private sealed class TestableImagePreviewer : ImagePreviewer
        {
            public Func<IFileSystemItem, CancellationToken, Task<Size?>>? SizeCalculator { get; set; }

            public Func<IFileSystemItem, CancellationToken, Task<ImageSource?>>? FullQualityHandler { get; set; }

            public Func<IFileSystemItem, CancellationToken, Task<ImageSource?>>? ThumbnailHandler { get; set; }

            public TestableImagePreviewer(IFileSystemItem item)
                : base(item)
            {
            }

            internal override Task<Size?> CalculateImageSizeAsync(IFileSystemItem item, CancellationToken cancellationToken) =>
                SizeCalculator is not null
                    ? SizeCalculator(item, cancellationToken)
                    : base.CalculateImageSizeAsync(item, cancellationToken);

            internal override Task<ImageSource?> LoadFullQualityImageAsync(IFileSystemItem item, CancellationToken cancellationToken) =>
                FullQualityHandler is not null
                    ? FullQualityHandler(item, cancellationToken)
                    : base.LoadFullQualityImageAsync(item, cancellationToken);

            internal override Task<ImageSource?> LoadThumbnailImageAsync(IFileSystemItem item, CancellationToken cancellationToken) =>
                ThumbnailHandler is not null
                    ? ThumbnailHandler(item, cancellationToken)
                    : base.LoadThumbnailImageAsync(item, cancellationToken);

            // Uninitialized dummy BitmapImages cannot be measured via COM, so the size
            // is resolved through the mocked CalculateImageSizeAsync instead.
            internal override Size? TryGetSourcePixelSize(ImageSource source) => null;
        }

        private sealed class TestableUnsupportedFilePreviewer : UnsupportedFilePreviewer
        {
            public Func<IFileSystemItem, CancellationToken, Task<string>>? ContentTypeResolver { get; set; }

            public Func<IFileSystemItem, CancellationToken, Task<ImageSource?>>? IconResolver { get; set; }

            public TestableUnsupportedFilePreviewer(IFileSystemItem item)
                : base(item)
            {
            }

            internal override Task<string> GetContentTypeAsync(IFileSystemItem item, CancellationToken cancellationToken) =>
                ContentTypeResolver is not null
                    ? ContentTypeResolver(item, cancellationToken)
                    : base.GetContentTypeAsync(item, cancellationToken);

            internal override Task<ImageSource?> GetIconPreviewAsync(IFileSystemItem item, CancellationToken cancellationToken) =>
                IconResolver is not null
                    ? IconResolver(item, cancellationToken)
                    : Task.FromResult<ImageSource?>(null);
        }

        // UnsupportedFilePreviewer out-of-order completion tests
        [TestMethod]
        public async Task UnsupportedFilePreviewer_CancelledTypeCompletesAfterItemB_DoesNotOverwriteMetadata()
        {
            // Simulate item A type/icon/folder-progress result completing after item B.
            var itemA = new MockFileSystemItem { Path = @"C:\test\itemA.xyz", Name = "itemA.xyz" };
            var itemB = new MockFileSystemItem { Path = @"C:\test\itemB.abc", Name = "itemB.abc" };

            var tcsTypeA = new TaskCompletionSource<string>();

            var previewer = new TestableUnsupportedFilePreviewer(itemA)
            {
                ContentTypeResolver = (item, cancellationToken) => item == itemA
                    ? tcsTypeA.Task // Item A's type resolution is delayed.
                    : Task.FromResult("Type B"),
            };

            var sizeLoaded = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            previewer.Preview.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UnsupportedFilePreviewData.FileSize))
                {
                    sizeLoaded.TrySetResult(previewer.Preview.FileSize);
                }
            };

            var ctsA = new CancellationTokenSource();
            var taskA = previewer.LoadPreviewAsync(ctsA.Token);
            ctsA.Cancel();

            previewer.Rebind(itemB, scalingFactor: 1.0);
            await previewer.LoadPreviewAsync(CancellationToken.None);

            // Complete the delayed Item A's type resolution after Item B has completed loading.
            tcsTypeA.SetResult("Stale Item A Type");
            try
            {
                await taskA; // This should throw due to cancellation.
            }
            catch (OperationCanceledException)
            {
            }

            // Stale Item A's type resolution should not overwrite Item B's metadata.
            Assert.AreEqual("itemB.abc", previewer.Preview.FileName);
            Assert.AreEqual("Type B", previewer.Preview.FileType);
            Assert.AreEqual(PreviewState.Loaded, previewer.State);
            Assert.AreEqual(1024UL, ((IFileSystemItem)itemB).FileSizeBytes);
            Assert.AreEqual(
                ReadableStringHelper.BytesToReadableString(1024),
                await sizeLoaded.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task UnsupportedFilePreviewer_OldIconCompletesAfterItemB_DoesNotOverwriteIcon(bool cancelOldRequest)
        {
            var itemA = new MockFileSystemItem { Path = @"C:\test\itemA.xyz" };
            var itemB = new MockFileSystemItem { Path = @"C:\test\itemB.xyz" };
            var iconA = CreateDummyImageSource();
            var iconB = CreateDummyImageSource();
            var iconLoadedA = new TaskCompletionSource<ImageSource?>();
            var previewer = new TestableUnsupportedFilePreviewer(itemA)
            {
                IconResolver = (item, token) => item == itemA
                    ? iconLoadedA.Task
                    : Task.FromResult<ImageSource?>(iconB),
            };

            using var ctsA = new CancellationTokenSource();
            var taskA = previewer.LoadIconPreviewAsync(itemA, ctsA.Token);
            if (cancelOldRequest)
            {
                ctsA.Cancel();
            }

            previewer.Rebind(itemB, 1.0);
            await previewer.LoadIconPreviewAsync(itemB, CancellationToken.None);
            Assert.AreSame(iconB, previewer.Preview.IconPreview);

            iconLoadedA.SetResult(iconA);
            if (cancelOldRequest)
            {
                await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => taskA);
            }
            else
            {
                await taskA;
            }

            Assert.AreSame(iconB, previewer.Preview.IconPreview);
        }

        [TestMethod]
        [DataRow(FolderScanState.Scanning)]
        [DataRow(FolderScanState.PartialError)]
        [DataRow(FolderScanState.Completed)]
        public void UnsupportedFilePreviewer_Rebind_ResetsFolderMetadataBeforeLoading(FolderScanState previousState)
        {
            var folderA = new FolderItem(@"C:\test\folderA", "folderA", @"C:\test\folderA");
            var folderB = new FolderItem(@"C:\test\folderB", "folderB", @"C:\test\folderB");
            var previewer = new UnsupportedFilePreviewer(folderA) { State = PreviewState.Loaded };
            previewer.Preview.IsFolder = true;
            previewer.Preview.FolderContents = "Old folder counts";
            previewer.Preview.FolderScanState = previousState;
            previewer.Preview.FileSize = "100 MB";

            previewer.Rebind(folderB, 1.0);

            Assert.AreEqual("folderB", previewer.Preview.FileName);
            Assert.IsTrue(previewer.Preview.IsFolder);
            Assert.IsNull(previewer.Preview.FolderContents);
            Assert.IsNull(previewer.Preview.FileSize);
            Assert.AreEqual(FolderScanState.Idle, previewer.Preview.FolderScanState);
            Assert.IsFalse(previewer.Preview.IsScanning);
            Assert.IsFalse(previewer.Preview.IsError);
            Assert.AreEqual(PreviewState.Loaded, previewer.State);

            previewer.Rebind(new MockFileSystemItem(), 1.0);
            Assert.IsFalse(previewer.Preview.IsFolder);
        }

        // ImagePreviewer out-of-order thumbnail test.
        [TestMethod]
        public async Task ImagePreviewer_CancelledThumbnailCompletesAfterNewItem_DoesNotOverwritePreview()
        {
            // Simulate item B's image completing and then item A's thumbnail completing
            // before item B presents.
            var itemA = new MockFileSystemItem { Path = @"C:\test\slowA.png", Name = "slowA.png" };
            var itemB = new MockFileSystemItem { Path = @"C:\test\fastB.png", Name = "fastB.png" };

            // Stays pending until we complete/cancel it.
            var tcsThumbnailA = new TaskCompletionSource<ImageSource?>();
            var dummyImageA = CreateDummyImageSource();
            var dummyImageB = CreateDummyImageSource();

            var previewer = new TestableImagePreviewer(itemA)
            {
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(null), // Full quality fails for both
                ThumbnailHandler = (item, token) => item == itemA
                    ? tcsThumbnailA.Task // Simulate slow thumbnail for item A (hangs)
                    : Task.FromResult<ImageSource?>(dummyImageB), // Succeeds for item B
                SizeCalculator = (item, token) => Task.FromResult<Size?>(new Size(1, 1)), // Keep size resolution off disk
            };

            var ctsA = new CancellationTokenSource();

            // Start load for A. Full quality load fails and thumbnail load hangs.
            var taskA = previewer.LoadPreviewAsync(ctsA.Token);

            // Cancel the load for A and rebind to B, simulating a navigation to a new item.
            ctsA.Cancel();
            previewer.Rebind(itemB, scalingFactor: 1.0);

            // Complete B's load (succeeds instantly).
            await previewer.LoadPreviewAsync(CancellationToken.None);
            var previewB = previewer.Preview;
            var frameB = previewer.Frame;

            // Now manually complete the delayed thumbnail load for the cancelled itemA.
            tcsThumbnailA.SetResult(dummyImageA);
            try
            {
                await taskA; // This should throw OperationCanceledException.
            }
            catch (OperationCanceledException)
            {
            }

            // NB: we must use AreSame here as the dummy images are uninitialized WinRT
            // COM objects and will not be equal by value.
            Assert.AreSame(
                previewB,
                previewer.Preview,
                "Delayed thumbnail from cancelled Item A must not overwrite the preview for Item B.");
            Assert.AreSame(frameB, previewer.Frame);
        }

        // ImagePreviewer out-of-order size test.
        [TestMethod]
        public async Task ImagePreviewer_CancelledSizeCompletesAfterNewItem_DoesNotOverwritePendingSize()
        {
            var itemASize = new Size(100, 100);
            var itemBSize = new Size(200, 200);

            // Simulate item B size completing then the cancelled size for item A
            // completes before the new size for item B is committed.
            var itemA = new MockFileSystemItem { Path = @"C:\test\itemA.png" };
            var itemB = new MockFileSystemItem { Path = @"C:\test\itemB.png" };

            var tcsSizeA = new TaskCompletionSource<Size?>();

            var previewer = new TestableImagePreviewer(itemA)
            {
                SizeCalculator = (item, token) => item == itemA
                    ? tcsSizeA.Task // Simulate slow size calculation for item A (hangs)
                    : Task.FromResult<Size?>(itemBSize), // Succeeds for item B
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(CreateDummyImageSource()),
            };

            var ctsA = new CancellationTokenSource();
            var sizeTaskA = previewer.GetPreviewSizeAsync(ctsA.Token);
            ctsA.Cancel();

            previewer.Rebind(itemB, scalingFactor: 1.0);
            await previewer.GetPreviewSizeAsync(CancellationToken.None);

            // Complete the delayed size calculation for the cancelled item A.
            tcsSizeA.SetResult(itemASize);
            try
            {
                await sizeTaskA; // This should throw due to cancellation.
            }
            catch (OperationCanceledException)
            {
            }

            // Commit the item B load.
            await previewer.LoadPreviewAsync(CancellationToken.None);

            // Item B's size must be committed, not the stale size from cancelled item A.
            Assert.AreEqual(
                itemBSize,
                previewer.ImageSize,
                "Late size calculation from Item A must not overwrite Item B's ImageSize.");
        }

        // ImagePreviewer rebind and state preservation test.
        [TestMethod]
        public async Task ImagePreviewer_Rebind_TransitionsToLoading_AndPreservesImageSize()
        {
            var itemASize = new Size(800, 600);

            // Rebinding retains the committed frame while its replacement loads.
            var itemA = new MockFileSystemItem { Path = @"C:\test\imageA.jpg", Name = "imageA.jpg" };
            var itemB = new MockFileSystemItem { Path = @"C:\test\imageB.jpg", Name = "imageB.jpg" };

            var previewer = new TestableImagePreviewer(itemA)
            {
                SizeCalculator = (item, token) => Task.FromResult<Size?>(itemASize),
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(CreateDummyImageSource()),
            };

            await previewer.LoadPreviewAsync(CancellationToken.None);
            var frame = previewer.Frame;
            previewer.Rebind(itemB, scalingFactor: 1.0);

            Assert.AreSame(frame, previewer.Frame);
            Assert.AreEqual(
                PreviewState.Loading,
                previewer.State,
                "State should transition to Loading after rebind.");
            Assert.AreEqual(
                itemASize,
                previewer.ImageSize,
                "ImageSize should be preserved after rebind until the new image is committed.");
        }

        // Decode failure and fallback tests
        [TestMethod]
        public async Task ImagePreviewer_FullQualityFailure_SucceedsViaThumbnailFallback()
        {
            // Full decode failure with a cached thumbnail succeeds via thumbnail.
            var item = new MockFileSystemItem();
            var expectedThumbnail = CreateDummyImageSource();

            var previewer = new TestableImagePreviewer(item)
            {
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(null), // Full quality fails
                ThumbnailHandler = (item, token) => Task.FromResult<ImageSource?>(expectedThumbnail), // Thumbnail succeeds
                SizeCalculator = (item, token) => Task.FromResult<Size?>(new Size(1, 1)), // Keep size resolution off disk
            };

            await previewer.LoadPreviewAsync(CancellationToken.None);

            Assert.AreEqual(
                PreviewState.Loaded,
                previewer.State,
                "State should be Loaded when thumbnail fallback succeeds.");
            Assert.AreSame(expectedThumbnail, previewer.Preview, "Preview should be set to the thumbnail.");
        }

        [TestMethod]
        public async Task ImagePreviewer_FullQualityFailureAndNullThumbnail_TransitionsToError()
        {
            // Full decode failure plus a null thumbnail enters Error, not Loaded state.
            var item = new MockFileSystemItem { Path = @"C:\test\sample.png" };

            var previewer = new TestableImagePreviewer(item)
            {
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(null),
                ThumbnailHandler = (item, token) => Task.FromResult<ImageSource?>(null),
            };

            await previewer.LoadPreviewAsync(CancellationToken.None);

            Assert.AreEqual(
                PreviewState.Error,
                previewer.State,
                "State must transition to Error when both full quality and thumbnail fail.");
        }

        [TestMethod]
        public async Task ImagePreviewer_GetPreviewSizeAsync_SuppressesImageSizeUpdate_WhenStateIsLoading()
        {
            var itemA = new MockFileSystemItem { Path = @"C:\test\imageA.jpg" };
            var itemB = new MockFileSystemItem { Path = @"C:\test\imageB.jpg" };

            var previewer = new TestableImagePreviewer(itemA)
            {
                SizeCalculator = (item, token) => Task.FromResult<Size?>(item == itemA ? new Size(1920, 1080) : new Size(800, 600)),
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(CreateDummyImageSource()),
            };

            await previewer.LoadPreviewAsync(CancellationToken.None);
            var frame = previewer.Frame;

            // Rebind to a new item, which should transition the state to Loading.
            previewer.Rebind(itemB, scalingFactor: 1.0);

            var previewSize = await previewer.GetPreviewSizeAsync(CancellationToken.None);

            Assert.AreSame(frame, previewer.Frame);
            Assert.AreEqual(new Size(800, 600), previewSize.MonitorSize);
            Assert.AreEqual(
                new Size(1920, 1080),
                previewer.ImageSize,
                "GetPreviewSizeAsync must not replace the visible frame's dimensions with the next item's size.");
        }

        [TestMethod]
        public async Task ImagePreviewer_GetPreviewSizeAsync_PreservesCommittedFrame_WhenStateIsLoaded()
        {
            var newSize = new Size(1200, 900);

            // A later size calculation must not rewrite size data paired with the
            // displayed source.
            var itemA = new MockFileSystemItem { Path = @"C:\test\imageA.jpg" };

            var previewer = new TestableImagePreviewer(itemA)
            {
                SizeCalculator = (item, token) => Task.FromResult<Size?>(new Size(800, 600)),
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(CreateDummyImageSource()),
            };

            await previewer.LoadPreviewAsync(CancellationToken.None);
            var frame = previewer.Frame;
            previewer.SizeCalculator = (item, token) => Task.FromResult<Size?>(newSize);

            var previewSize = await previewer.GetPreviewSizeAsync(CancellationToken.None);

            Assert.AreSame(frame, previewer.Frame);
            Assert.AreEqual(newSize, previewSize.MonitorSize);
            Assert.AreEqual(
                new Size(800, 600),
                previewer.ImageSize,
                "Window sizing must not rewrite the committed source's intrinsic dimensions.");
        }

        [TestMethod]
        public async Task ImagePreviewer_FrameNotifications_ExposeCoherentQueryProperties()
        {
            var itemA = new MockFileSystemItem();
            var itemB = new MockFileSystemItem();
            var sourceA = CreateDummyImageSource();
            var sourceB = CreateDummyImageSource();
            var sizeA = new Size(800, 600);
            var sizeB = new Size(32, 32);
            var previewer = new TestableImagePreviewer(itemA)
            {
                SizeCalculator = (item, token) => Task.FromResult<Size?>(item == itemA ? sizeA : sizeB),
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(item == itemA ? sourceA : sourceB),
            };

            await previewer.LoadPreviewAsync(CancellationToken.None);
            var oldFrame = previewer.Frame;
            var notifications = 0;
            previewer.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName is nameof(ImagePreviewer.Frame) or nameof(ImagePreviewer.Preview) or nameof(ImagePreviewer.ImageSize))
                {
                    notifications++;
                    Assert.IsNotNull(previewer.Frame);
                    Assert.AreSame(sourceB, previewer.Frame.Source);
                    Assert.AreEqual(sizeB, previewer.Frame.PixelSize);
                    Assert.AreSame(previewer.Frame.Source, previewer.Preview);
                    Assert.AreEqual(previewer.Frame.PixelSize, previewer.ImageSize);
                }
            };

            previewer.Rebind(itemB, 1.0);
            await previewer.LoadPreviewAsync(CancellationToken.None);

            Assert.AreEqual(3, notifications);
            Assert.IsNotNull(oldFrame);
            Assert.AreSame(sourceA, oldFrame.Source);
            Assert.AreEqual(sizeA, oldFrame.PixelSize);
        }

        [TestMethod]
        public async Task ImagePreviewer_FailedReplacement_ClearsFrameAndQueryProperties()
        {
            var previewer = new TestableImagePreviewer(new MockFileSystemItem())
            {
                SizeCalculator = (item, token) => Task.FromResult<Size?>(null),
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(CreateDummyImageSource()),
                ThumbnailHandler = (item, token) => Task.FromResult<ImageSource?>(null),
            };

            await previewer.LoadPreviewAsync(CancellationToken.None);
            Assert.IsNotNull(previewer.Frame);
            Assert.IsNull(previewer.ImageSize);
            previewer.Rebind(new MockFileSystemItem(), 1.0);
            previewer.FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(null);
            previewer.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName is nameof(ImagePreviewer.Frame) or nameof(ImagePreviewer.Preview) or nameof(ImagePreviewer.ImageSize))
                {
                    Assert.IsNull(previewer.Frame);
                    Assert.IsNull(previewer.Preview);
                    Assert.IsNull(previewer.ImageSize);
                }
            };

            await previewer.LoadPreviewAsync(CancellationToken.None);

            Assert.IsNull(previewer.Frame);
            Assert.IsNull(previewer.Preview);
            Assert.IsNull(previewer.ImageSize);
            Assert.AreEqual(PreviewState.Error, previewer.State);
        }

        // UnsupportedFilePreviewer race condition test.
        [TestMethod]
        public void UnsupportedFilePreviewer_Rebind_ClearsAsyncPropertiesImmediately()
        {
            DateTime updatedDateModified = new(2024, 4, 4);

            var itemA = new MockFileSystemItem
            {
                Path = @"C:\test\fileA.xyz",
                Name = "fileA.xyz",
                DateModified = new DateTime(2022, 2, 2),
            };

            var itemB = new MockFileSystemItem
            {
                Path = @"C:\test\fileB.abc",
                Name = "fileB.abc",
                DateModified = updatedDateModified,
            };

            var previewer = new UnsupportedFilePreviewer(itemA);
            previewer.Preview.FileType = "Old Type A";
            previewer.Preview.FileSize = "100 MB";

            // Rebind to a new item, which should clear the async properties immediately.
            previewer.Rebind(itemB, double.NaN);

            Assert.AreEqual("fileB.abc", previewer.Preview.FileName);
            Assert.AreEqual(
                updatedDateModified,
                previewer.Item.DateModified,
                "DateModified should be updated immediately after rebind.");
            Assert.IsNull(previewer.Preview.FileType, "Stale FileType should be cleared immediately after rebind.");
            Assert.IsNull(previewer.Preview.FileSize, "Stale FileSize should be cleared immediately after rebind.");
        }
    }
}
