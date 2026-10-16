// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls;

internal sealed class IconPresentationState<T>
    where T : class
{
    public T? PlacementFallback { get; set; }

    public T? RequestFallback { get; private set; }

    public T? ResolvedSource { get; private set; }

    public bool HasResolvedSource { get; private set; }

    public bool ResolvedSourceExpectsImage { get; private set; }

    public void BeginSourceChange()
    {
        RequestFallback = null;
        ResolvedSource = null;
        HasResolvedSource = false;
        ResolvedSourceExpectsImage = false;
    }

    public void SetRequestFallback(T? source) => RequestFallback = source;

    public void SetResolvedSource(T? source, bool expectsImageSource)
    {
        ResolvedSource = source;
        HasResolvedSource = true;
        ResolvedSourceExpectsImage = expectsImageSource;
    }

    public T? SelectSource(bool preferFallbackForResolvedSource)
    {
        var fallback = PlacementFallback ?? RequestFallback;
        return !HasResolvedSource || (preferFallbackForResolvedSource && fallback is not null)
            ? fallback
            : ResolvedSource;
    }
}
