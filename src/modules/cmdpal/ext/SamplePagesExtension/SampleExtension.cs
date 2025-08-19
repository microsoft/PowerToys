// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace SamplePagesExtension;

// [ComVisible(true)]
// [ComDefaultInterface(typeof(IExtension))]
[Guid("6112D28D-6341-45C8-92C3-83ED55853A9F")]

// [global::WinRT.WinRTExposedType]
public sealed partial class SampleExtension : IExtension, IDisposable// , IDynamicInterfaceCastable
{
    private bool disposed;

    public event TypedEventHandler<IExtension, object> Disposed;

    private readonly SamplePagesCommandsProvider _provider = new();

    public SampleExtension()
    {
    }

    public object GetProvider(ProviderType providerType)
    {
        switch (providerType)
        {
            case ProviderType.Commands:
                return _provider;
            default:
                return null;
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            Disposed?.Invoke(this, null);
            _provider.Dispose();
            disposed = true;
        }
    }

    // public bool IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented)
    // {
    //    Debug.WriteLine($"{interfaceType}");
    //    return true;
    // }

    // public RuntimeTypeHandle GetInterfaceImplementation(RuntimeTypeHandle interfaceType) => throw new NotImplementedException();
}
