// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CmdPal.Ext.PowerToys.Host;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.PowerToys;

[ComVisible(true)]
[Guid("F0A8B809-CE2C-475A-935F-64A0348B1D29")]
[ComDefaultInterface(typeof(IExtension))]
public sealed class PowerToysExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _lifetime;
    private readonly PowerToysCommandsProvider _provider = new();

    public PowerToysExtension(ManualResetEvent extensionDisposedEvent)
    {
        _lifetime = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose()
    {
        _lifetime.Set();
    }
}
