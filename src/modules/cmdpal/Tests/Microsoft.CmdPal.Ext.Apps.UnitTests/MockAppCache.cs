// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Mock implementation of IAppCache for unit testing.
/// </summary>
public class MockAppCache : IAppCache
{
    private readonly List<Win32Program> _win32s = new();
    private readonly List<IUWPApplication> _uwps = new();
    private bool _disposed;
    private bool _shouldReload;

    /// <summary>
    /// Gets the collection of Win32 programs.
    /// </summary>
    public IList<Win32Program> Win32s => _win32s.AsReadOnly();

    /// <summary>
    /// Gets the collection of UWP applications.
    /// </summary>
    public IList<IUWPApplication> UWPs => _uwps.AsReadOnly();

    /// <summary>
    /// Determines whether the cache should be reloaded.
    /// </summary>
    /// <returns>True if cache should be reloaded, false otherwise.</returns>
    public bool ShouldReload() => _shouldReload;

    /// <summary>
    /// Resets the reload flag.
    /// </summary>
    public void ResetReloadFlag() => _shouldReload = false;

    /// <summary>
    /// Asynchronously refreshes the cache.
    /// </summary>
    /// <returns>A task representing the asynchronous refresh operation.</returns>
    public async Task RefreshAsync()
    {
        // Simulate minimal async operation for testing
        await Task.Delay(1);
    }

    /// <summary>
    /// Adds a Win32 program to the cache.
    /// </summary>
    /// <param name="program">The Win32 program to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when program is null.</exception>
    public void AddWin32Program(Win32Program program)
    {
        ArgumentNullException.ThrowIfNull(program);

        _win32s.Add(program);
    }

    /// <summary>
    /// Adds a UWP application to the cache.
    /// </summary>
    /// <param name="app">The UWP application to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when app is null.</exception>
    public void AddUWPApplication(IUWPApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        _uwps.Add(app);
    }

    /// <summary>
    /// Clears all applications from the cache.
    /// </summary>
    public void ClearAll()
    {
        _win32s.Clear();
        _uwps.Clear();
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clean up managed resources
                _win32s.Clear();
                _uwps.Clear();
            }

            _disposed = true;
        }
    }
}
