// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;

namespace PowerToysExtension;

[Guid("7EC02C7D-8F98-4A2E-9F23-B58C2C2F2B17")]
public sealed partial class PowerToysExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly PowerToysExtensionCommandsProvider _provider = new();

    public PowerToysExtension(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
        Logger.LogInfo("PowerToysExtension constructed.");
    }

    public object? GetProvider(ProviderType providerType)
    {
        Logger.LogInfo($"GetProvider requested: {providerType}");
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose()
    {
        Logger.LogInfo("PowerToysExtension disposing; signalling exit.");
        this._extensionDisposedEvent.Set();
    }
}
