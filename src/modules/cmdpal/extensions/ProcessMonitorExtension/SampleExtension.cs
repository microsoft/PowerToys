// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Windows.CommandPalette.Extensions;

namespace ProcessMonitorExtension;

[ComVisible(true)]
[Guid("8BD7A6C4-7185-4426-AE8D-61E438A3E740")]
[ComDefaultInterface(typeof(IExtension))]
public sealed class SampleExtension : IExtension
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    public SampleExtension(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
    }

    public object GetProvider(ProviderType providerType)
    {
        switch (providerType)
        {
            case ProviderType.Commands:
                return new ProcessMonitorActionProvider();
            default:
                return null;
        }
    }

    public void Dispose()
    {
        this._extensionDisposedEvent.Set();
    }
}
