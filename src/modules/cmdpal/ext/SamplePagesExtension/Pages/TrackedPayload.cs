// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SamplePagesExtension.Pages;

/// <summary>
/// Holds the ballast bytes for a single list item. The finalizer is the signal
/// that matters: it runs only once nothing references this payload any more,
/// including the host's proxy for the list item that owns it.
/// </summary>
internal sealed class TrackedPayload
{
    private readonly byte[] _ballast;

    public TrackedPayload(int bytes)
    {
        _ballast = new byte[bytes];
        LeakTracker.Payloads.OnCreated();
    }

    ~TrackedPayload() => LeakTracker.Payloads.OnReleased();

    public int Size => _ballast.Length;
}
