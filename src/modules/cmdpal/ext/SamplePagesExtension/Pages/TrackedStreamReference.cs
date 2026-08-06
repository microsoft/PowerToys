// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace SamplePagesExtension.Pages;

/// <summary>
/// An <see cref="IRandomAccessStreamReference"/> we own, so the moment the host
/// releases its proxy is observable.
/// </summary>
/// <remarks>
/// <para>
/// <c>RandomAccessStreamReference.CreateFromStream</c> hands back a system object
/// with no way to see when it dies. Implementing the interface here means the
/// object is a CCW this extension owns: it stays alive exactly as long as the
/// host holds a reference, and its finalizer is therefore a precise signal that
/// the host has let go.
/// </para>
/// <para>
/// The finalizer also disposes the backing stream, which is what actually returns
/// the bytes - they live in a WinRT stream, not on the managed heap, so nothing
/// else in the process would free them.
/// </para>
/// </remarks>
internal sealed partial class TrackedStreamReference : IRandomAccessStreamReference
{
    private readonly InMemoryRandomAccessStream _stream;
    private readonly IRandomAccessStreamReference _inner;
    private readonly long _bytes;

    public TrackedStreamReference(InMemoryRandomAccessStream stream, long bytes)
    {
        _stream = stream;
        _bytes = bytes;

        // Reads are delegated to a system reference; the point of this wrapper is
        // purely that we own the object the host takes a proxy on, so we can see
        // when it lets go.
        _inner = RandomAccessStreamReference.CreateFromStream(stream);

        LeakTracker.Streams.OnCreated(bytes);
    }

    ~TrackedStreamReference()
    {
        LeakTracker.Streams.OnReleased(_bytes);

        try
        {
            _stream.Dispose();
        }
        catch (Exception)
        {
            // Finalizers must not throw; a stream already torn down during
            // shutdown is not interesting here.
        }
    }

    public IAsyncOperation<IRandomAccessStreamWithContentType> OpenReadAsync() => _inner.OpenReadAsync();
}
