// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace MonitorPowerExtension;

[ComVisible(true)]
[Guid("C93D29D1-E3D1-46AA-A551-1D7BD11C8B62")]
[ComDefaultInterface(typeof(IExtension))]
public sealed partial class MonitorPowerExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly MonitorPowerCommandsProvider _provider = new();

    public MonitorPowerExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType == ProviderType.Commands ? _provider : null;
    }

    public void Dispose()
    {
        _extensionDisposedEvent.Set();
    }
}
