// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CmdPal.Extensions;

namespace EverythingExtension;

[ComVisible(true)]
[Guid("c4d344ce-480a-4ef5-9875-96e7bf2b6992")]
[ComDefaultInterface(typeof(IExtension))]
public sealed partial class SampleExtension : IExtension
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
                return new EverythingExtensionActionsProvider();
            default:
                return null;
        }
    }

    public void Dispose()
    {
        this._extensionDisposedEvent.Set();
    }
}
