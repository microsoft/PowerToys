// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class IconDataViewModelTests
{
    [TestMethod]
    public void InitializeProperties_SnapshotsExtensionStreamLocally()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var sourceReference = new ToggleableStreamReference(payload);
        var viewModel = new IconDataViewModel(new IconData(sourceReference));

        viewModel.InitializeProperties();
        sourceReference.ThrowOnOpen = true;

        Assert.IsTrue(viewModel.HasIcon);
        Assert.IsNotNull(viewModel.Data);
    }

    [TestMethod]
    public void InitializeProperties_WhenStreamCannotBeCopied_TreatsDataAsUnavailable()
    {
        var sourceReference = new ToggleableStreamReference([9, 8, 7])
        {
            ThrowOnOpen = true,
        };

        var viewModel = new IconDataViewModel(new IconData(sourceReference));

        viewModel.InitializeProperties();

        Assert.IsFalse(viewModel.HasIcon);
        Assert.IsNull(viewModel.Data);
        Assert.IsTrue(string.IsNullOrEmpty(viewModel.Icon));
    }

    private static byte[] ReadAllBytes(IRandomAccessStreamReference? streamReference)
    {
        Assert.IsNotNull(streamReference);

        using var stream = streamReference.OpenReadAsync().AsTask().GetAwaiter().GetResult();
        using var reader = new DataReader(stream);
        var bytesToRead = checked((uint)stream.Size);
        reader.LoadAsync(bytesToRead).AsTask().GetAwaiter().GetResult();

        var bytes = new byte[bytesToRead];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private sealed partial class ToggleableStreamReference(byte[] payload) : IRandomAccessStreamReference
    {
        public bool ThrowOnOpen { get; set; }

        public IAsyncOperation<IRandomAccessStreamWithContentType> OpenReadAsync()
        {
            ObjectDisposedException.ThrowIf(ThrowOnOpen, nameof(ToggleableStreamReference));

            var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(payload);
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.DetachStream();
            stream.Seek(0);

            return RandomAccessStreamReference.CreateFromStream(stream).OpenReadAsync();
        }
    }
}
