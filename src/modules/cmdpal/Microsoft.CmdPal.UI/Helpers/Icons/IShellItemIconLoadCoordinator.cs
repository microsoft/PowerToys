// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IShellItemIconLoadCoordinator
{
    /// <summary>
    /// Publishes a resolved Shell identity and decides whether this worker owns its load.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when another request already owns or completed the canonical
    /// load. The caller must forward <paramref name="sharedTask"/> and release its worker.
    /// </returns>
    bool TryJoinExistingLoad(
        LocatedShellIcon locatedIcon,
        [MaybeNullWhen(false)] out Task<IconSource?> sharedTask);
}
