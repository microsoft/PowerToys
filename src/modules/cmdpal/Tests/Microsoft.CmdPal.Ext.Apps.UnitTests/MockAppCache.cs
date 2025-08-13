// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Mock implementation of IAppCache for unit testing
/// </summary>
public class MockAppCache : IAppCache
{
    private bool _disposed;
    private bool _shouldReload;

    public IList<Win32Program> Win32s { get; set; } = new List<Win32Program>();

    public IList<IUWPApplication> UWPs { get; set; } = new List<IUWPApplication>();

    public bool ShouldReload() => _shouldReload;

    public void ResetReloadFlag() => _shouldReload = false;

    public void SetShouldReload(bool shouldReload) => _shouldReload = shouldReload;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clean up managed resources if needed
                Win32s?.Clear();
                UWPs?.Clear();
            }

            _disposed = true;
        }
    }
}
