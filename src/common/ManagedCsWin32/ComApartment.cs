// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ManagedCsWin32;

/// <summary>
/// Provides COM apartment initialization and teardown for the current thread.
/// </summary>
public sealed class ComApartment : IDisposable
{
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComApartment"/> class.
    /// Calls CoInitializeEx with STA or MTA based on <paramref name="sta"/>.
    /// </summary>
    /// <param name="sta">True for STA, false for MTA.</param>
    public ComApartment(bool sta = true)
    {
        const uint COINIT_APARTMENTTHREADED = 0x2;
        const uint COINIT_MULTITHREADED = 0x0;

        var hr = Ole32.CoInitializeEx(0, sta ? COINIT_APARTMENTTHREADED : COINIT_MULTITHREADED);
        if (hr >= 0)
        {
            _initialized = true;
        }
    }

    /// <summary>
    /// Uninitializes COM if this instance previously succeeded initialization.
    /// </summary>
    public void Dispose()
    {
        if (_initialized)
        {
            Ole32.CoUninitialize();
            _initialized = false;
        }

        GC.SuppressFinalize(this);
    }
}
