// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Windows.DevPal.SDK;

namespace AzureResourcesExtension;

[ComVisible(true)]
[Guid("55B0369C-C696-42DD-A98F-31A3321B4E85")]
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
                return new AzureResourcesCommandsProvider();
            default:
                return null;
        }
    }

    public void Dispose()
    {
        this._extensionDisposedEvent.Set();
    }
}
