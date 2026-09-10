// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

// One immutable snapshot keeps the two bounds together when extension notifications arrive off the UI thread.
public sealed record DockLabelWidthConstraints(DockLabelLength? Minimum, DockLabelLength? Maximum)
{
    public static DockLabelWidthConstraints Default { get; } = new(null, null);

    public bool UsesCharacters => Minimum?.InCharacters == true || Maximum?.InCharacters == true;

    internal static DockLabelWidthConstraints FromProperties(IDictionary<string, object?>? properties)
    {
        object? minimum = null;
        object? maximum = null;
        properties?.TryGetValue(WellKnownExtensionAttributes.DockMinLabelWidth, out minimum);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockMaxLabelWidth, out maximum);

        var minLength = DockLabelLength.Parse(minimum);
        var maxLength = DockLabelLength.Parse(maximum);
        return minLength is null && maxLength is null ? Default : new(minLength, maxLength);
    }

    public (double Minimum, double Maximum) Resolve(double characterWidth, double defaultMinimum, double defaultMaximum)
    {
        var minimum = Minimum?.Resolve(characterWidth);
        var maximum = Maximum?.Resolve(characterWidth);

        // Compare after resolving: a pair can mix DIPs, ch, or sqh, and text scaling can change its ordering.
        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
        {
            return (defaultMinimum, defaultMaximum);
        }

        // An explicit bound takes precedence over the opposite default. In particular, a requested
        // minimum above the default cap must not be rejected just because no maximum was provided.
        var min = minimum ?? Math.Min(defaultMinimum, maximum ?? defaultMaximum);
        var max = maximum ?? Math.Max(defaultMaximum, min);
        return (min, max);
    }
}
