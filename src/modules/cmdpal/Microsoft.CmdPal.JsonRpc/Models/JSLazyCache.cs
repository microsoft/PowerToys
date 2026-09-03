// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.JsonRpc.Models;

internal sealed partial class JSLazyCache<T> : IDisposable
{
    private readonly object _lock = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _dispose;
    private T _value = default!;
    private bool _hasValue;
    private bool _disposed;

    internal JSLazyCache(Func<T> factory, Action<T>? dispose = null)
    {
        _factory = factory;
        _dispose = dispose;
    }

    internal T Value
    {
        get
        {
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (!_hasValue)
                {
                    _value = _factory();
                    _hasValue = true;
                }

                return _value;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeCreatedValue();
        }
    }

    internal void Reset()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            // The host may still own the previous proxy, so invalidation only releases the cache reference.
            _value = default!;
            _hasValue = false;
        }
    }

    internal static void DisposeValue(T value)
    {
        if (value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void DisposeCreatedValue()
    {
        if (_hasValue)
        {
            _dispose?.Invoke(_value);
        }
    }
}
