// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace CompatibilityExtension;

[ComVisible(true)]
[Guid(Baseline.Clsid)]
[ComDefaultInterface(typeof(IExtension))]
public sealed partial class Extension : IExtension, IDisposable
{
    private readonly ManualResetEvent _disposed;
    private readonly CompatibilityCommandsProvider _provider = new();
    private int _isDisposed;

    public Extension(ManualResetEvent disposed)
    {
        _disposed = disposed;
    }

    public object? GetProvider(ProviderType providerType) =>
        providerType == ProviderType.Commands ? _provider : null;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            _provider.Dispose();
        }
        finally
        {
            _disposed.Set();
        }
    }
}
