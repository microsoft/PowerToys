// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

/// <summary>
/// It's wil::scope_exit, but for C#.
/// </summary>
internal sealed class ScopeExit : IDisposable
{
    private readonly Action _onDispose;

    public ScopeExit(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        _onDispose();
    }
}
