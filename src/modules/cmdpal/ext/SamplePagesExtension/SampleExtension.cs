// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using IExtension = Microsoft.CommandPalette.Extensions.Toolkit.Local.IExtension;

namespace SamplePagesExtension;

[Guid("6112D28D-6341-45C8-92C3-83ED55853A9F")]
[GeneratedComClass]
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed partial class SampleExtension : IExtension
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly SamplePagesCommandsProvider _provider = new();

    private static readonly ComWrappers _comWrappers = new StrategyBasedComWrappers();

    public SampleExtension(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
    }

    public IntPtr GetProvider(ProviderType providerType)
    {
        switch (providerType)
        {
            case ProviderType.Commands:
                return _comWrappers.GetOrCreateComInterfaceForObject(_provider, CreateComInterfaceFlags.None);
            default:
                return IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        this._extensionDisposedEvent.Set();
    }
}
