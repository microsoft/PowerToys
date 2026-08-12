// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Coalesces IconBox refresh requests until every dispatch requirement is available.
/// </summary>
internal struct IconRefreshState
{
    private bool _isPending;
    private IconRequestReason _reason;

    public void Request(bool hasSource, IconRequestReason reason)
    {
        if (!hasSource)
        {
            Clear();
            return;
        }

        _isPending = true;
        _reason |= reason;
    }

    public bool TryConsume(
        bool isLoaded,
        bool hasSource,
        bool hasHandler,
        out IconRequestReason reason)
    {
        if (!_isPending || !isLoaded || !hasSource || !hasHandler)
        {
            reason = IconRequestReason.None;
            return false;
        }

        reason = _reason;
        Clear();
        return true;
    }

    public void Clear()
    {
        _isPending = false;
        _reason = IconRequestReason.None;
    }
}
