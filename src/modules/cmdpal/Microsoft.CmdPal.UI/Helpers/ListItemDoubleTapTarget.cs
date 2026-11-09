// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class ListItemDoubleTapTarget
{
    internal static ListItemViewModel? Resolve<TElement, TContainer>(
        TElement? source,
        TElement owner,
        bool singleClickActivates,
        Func<TElement, TElement?> getParent,
        Func<TContainer, ListItemViewModel?> getItem)
        where TElement : class
        where TContainer : class, TElement
    {
        if (singleClickActivates)
        {
            return null;
        }

        for (var current = source; current is not null && !ReferenceEquals(current, owner); current = getParent(current))
        {
            if (current is TContainer container)
            {
                var item = getItem(container);
                return item is { IsInteractive: true } ? item : null;
            }
        }

        return null;
    }
}
