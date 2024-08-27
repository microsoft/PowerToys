// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Windows.CommandPalette.Extensions;

namespace MediaControlsExtension;

[ComVisible(true)]
[Guid("bb60a98a-0197-4378-9b40-b684f4068d1d")]
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
                return new MediaActionsProvider();
            default:
                return null;
        }
    }

    public void Dispose()
    {
        this._extensionDisposedEvent.Set();
    }
}
