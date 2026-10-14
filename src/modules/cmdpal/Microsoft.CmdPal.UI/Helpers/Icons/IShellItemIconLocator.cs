// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IShellItemIconLocator
{
    bool TryLocate(
        ShellItemIconRequest request,
        [MaybeNullWhen(false)] out LocatedShellIcon locatedIcon);
}
