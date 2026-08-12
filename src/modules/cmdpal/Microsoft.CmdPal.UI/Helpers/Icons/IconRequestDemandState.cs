// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.CmdPal.UI.Helpers;

internal struct IconRequestDemandState
{
    private IconLoadDemand? _loadDemand;
    private int _released;

    public void Attach(IconLoadDemand loadDemand)
    {
        loadDemand.AddRequester();
        if (Volatile.Read(ref _released) != 0)
        {
            loadDemand.RemoveRequester();
            return;
        }

        var existing = Interlocked.CompareExchange(ref _loadDemand, loadDemand, null);
        Debug.Assert(existing is null, "An icon request can track only one load.");
        if (existing is not null)
        {
            loadDemand.RemoveRequester();
            return;
        }

        if (Volatile.Read(ref _released) != 0
            && ReferenceEquals(Interlocked.CompareExchange(ref _loadDemand, null, loadDemand), loadDemand))
        {
            loadDemand.RemoveRequester();
        }
    }

    public void Release()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _loadDemand, null)?.RemoveRequester();
    }
}
