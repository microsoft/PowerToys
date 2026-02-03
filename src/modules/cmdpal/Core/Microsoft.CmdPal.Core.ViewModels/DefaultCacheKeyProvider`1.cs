// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Core.ViewModels;

internal sealed class DefaultCacheKeyProvider<TModel> : ICacheKeyProvider<TModel>
    where TModel : class
{
    public CacheKey GetKey(TModel model)
    {
        // 1) Explicit stable ID (future-proof, best option)
        if (model is IHasStableId keyed && !string.IsNullOrEmpty(keyed.StableId))
        {
            return CacheKey.Stable(keyed.StableId);
        }

        // 2) Try COM identity (WinRT / COM proxies)
        if (TryGetComIdentity(model, out var ptr))
        {
            return CacheKey.ComIdentity(ptr);
        }

        // 3) Fallback: reference identity
        return CacheKey.Reference(model);
    }

    private static bool TryGetComIdentity(object obj, out nint identityPtrValue)
    {
        identityPtrValue = 0;
        nint unk = 0;

        try
        {
            // This can throw for non-COM objects. That’s fine; we fall back.
            unk = Marshal.GetIUnknownForObject(obj);
            identityPtrValue = unk; // pointer value used as key
            return identityPtrValue != 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (unk != 0)
            {
                Marshal.Release(unk); // must release refcount
            }
        }
    }
}
