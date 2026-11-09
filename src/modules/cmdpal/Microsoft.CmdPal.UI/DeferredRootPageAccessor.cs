// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// Defers access to <see cref="IRootPageService"/> until the root page is
/// actually requested, avoiding a construction-time DI cycle for built-in
/// providers that expose the root page as a dock band.
/// </summary>
internal sealed class DeferredRootPageAccessor(Func<IRootPageService> getRootPageService) : IRootPageAccessor
{
    private readonly Func<IRootPageService> _getRootPageService = getRootPageService;

    public IPage GetRootPage() => _getRootPageService().GetRootPage();
}
