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
using Peek.Common.Models;
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

            public long FileSizeBytes { get; set; } = 1024;

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

            public Func<IFileSystemItem, CancellationToken, Task>? IconLoader { get; set; }

            public TestableUnsupportedFilePreviewer(IFileSystemItem item)
                : base(item)
            {
            }

            internal override Task<string> GetContentTypeAsync(IFileSystemItem item, CancellationToken cancellationToken) =>
                ContentTypeResolver is not null
                    ? ContentTypeResolver(item, cancellationToken)
                    : base.GetContentTypeAsync(item, cancellationToken);

            internal override Task LoadIconPreviewAsync(IFileSystemItem item, CancellationToken cancellationToken) =>
                IconLoader is not null
                    ? IconLoader(item, cancellationToken)
                    : Task.CompletedTask; // Skip disk shell icon lookup in unit tests
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
        public void ImagePreviewer_Rebind_TransitionsToLoading_AndPreservesImageSize()
        {
            var itemASize = new Size(800, 600);

            // Rebinding a loaded image immediately enters Loading state and keeps old Preview and ImageSize.
            var itemA = new MockFileSystemItem { Path = @"C:\test\imageA.jpg", Name = "imageA.jpg" };
            var itemB = new MockFileSystemItem { Path = @"C:\test\imageB.jpg", Name = "imageB.jpg" };

            var previewer = new ImagePreviewer(itemA)
            {
                State = PreviewState.Loaded,
                ImageSize = itemASize,
            };

            previewer.Rebind(itemB, scalingFactor: 1.0);

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
                State = PreviewState.Loaded,
                ImageSize = new Size(1920, 1080),
                SizeCalculator = (item, token) => Task.FromResult<Size?>(new Size(800, 600)),
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(CreateDummyImageSource()),
            };

            // Rebind to a new item, which should transition the state to Loading.
            previewer.Rebind(itemB, scalingFactor: 1.0);

            var previewSize = await previewer.GetPreviewSizeAsync(CancellationToken.None);

            Assert.AreEqual(
                new Size(1920, 1080),
                previewer.ImageSize,
                "GetPreviewSizeAsync should return the preserved ImageSize when state is Loading.");
        }

        [TestMethod]
        public async Task ImagePreviewer_GetPreviewSizeAsync_UpdatesImageSizeImmediately_WhenStateIsLoaded()
        {
            var newSize = new Size(1200, 900);

            // Simulate moving window from one display to another (DPI change) while
            // displaying the same image.
            var itemA = new MockFileSystemItem { Path = @"C:\test\imageA.jpg" };

            var previewer = new TestableImagePreviewer(itemA)
            {
                State = PreviewState.Loaded,
                ImageSize = new Size(800, 600),
                SizeCalculator = (item, token) => Task.FromResult<Size?>(newSize), // Simulate a new size calculation
                FullQualityHandler = (item, token) => Task.FromResult<ImageSource?>(CreateDummyImageSource()),
            };

            // DPI change simulation.
            var previewSize = await previewer.GetPreviewSizeAsync(CancellationToken.None);

            Assert.AreEqual(
                newSize,
                previewer.ImageSize,
                "ImageSize should be updated immediately when state is Loaded.");
        }

        // UnsupportedFilePreviewer race condition test.
        [TestMethod]
        public async Task UnsupportedFilePreviewer_Rebind_ClearsAsyncPropertiesImmediately()
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
