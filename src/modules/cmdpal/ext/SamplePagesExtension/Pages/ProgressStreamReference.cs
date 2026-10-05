// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace SamplePagesExtension.Pages;

internal sealed partial class ProgressStreamReference : IRandomAccessStreamReference
{
    private readonly InMemoryRandomAccessStream _stream;
    private readonly int _delayMilliseconds;

    public ProgressStreamReference(InMemoryRandomAccessStream stream, int delayMilliseconds)
    {
        _stream = stream;
        _delayMilliseconds = delayMilliseconds;
    }

    public IAsyncOperation<IRandomAccessStreamWithContentType> OpenReadAsync()
    {
        return OpenReadCoreAsync().AsAsyncOperation();
    }

    private async Task<IRandomAccessStreamWithContentType> OpenReadCoreAsync()
    {
        if (_delayMilliseconds > 0)
        {
            await Task.Delay(_delayMilliseconds).ConfigureAwait(false);
        }

        // Each reader gets its own cursor and lifetime over the immutable PNG data.
        using var clone = _stream.CloneStream();
        return await RandomAccessStreamReference.CreateFromStream(clone).OpenReadAsync();
    }
}
