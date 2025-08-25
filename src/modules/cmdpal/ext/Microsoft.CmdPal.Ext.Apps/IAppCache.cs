// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>
/// Interface for application cache that provides access to Win32 and UWP applications.
/// </summary>
public interface IAppCache : IDisposable
{
    /// <summary>
    /// Gets the collection of Win32 programs.
    /// </summary>
    IList<Win32Program> Win32s { get; }

    /// <summary>
    /// Gets the collection of UWP applications.
    /// </summary>
    IList<IUWPApplication> UWPs { get; }

    /// <summary>
    /// Determines whether the cache should be reloaded.
    /// </summary>
    /// <returns>True if cache should be reloaded, false otherwise.</returns>
    bool ShouldReload();

    /// <summary>
    /// Resets the reload flag.
    /// </summary>
    void ResetReloadFlag();
}
