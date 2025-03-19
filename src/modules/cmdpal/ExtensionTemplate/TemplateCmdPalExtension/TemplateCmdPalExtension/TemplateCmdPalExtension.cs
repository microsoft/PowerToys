// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace TemplateCmdPalExtension;

[ComVisible(true)]
[Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
[ComDefaultInterface(typeof(IExtension))]
public sealed partial class TemplateCmdPalExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly TemplateCmdPalExtensionCommandsProvider _provider = new();

    public TemplateCmdPalExtension(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose() => this._extensionDisposedEvent.Set();
}
