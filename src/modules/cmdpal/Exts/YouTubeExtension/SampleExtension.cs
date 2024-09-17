// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CmdPal.Extensions;

namespace YouTubeExtension;

[ComVisible(true)]
[Guid("95696eff-5c44-41ad-8f64-c2182ec9e3fd")]
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
                return new YouTubeExtensionActionsProvider();
            default:
                return null;
        }
    }

    public void Dispose()
    {
        this._extensionDisposedEvent.Set();
    }
}
