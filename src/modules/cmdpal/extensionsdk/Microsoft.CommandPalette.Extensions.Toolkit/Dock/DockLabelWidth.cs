// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A Dock label reservation or limit, expressed in DIPs, character units, or a literal text sample.
/// </summary>
/// <remarks>
/// Toolkit helpers store a double or string in the extended attributes, not this managed object.
/// </remarks>
public sealed class DockLabelWidth
{
    private DockLabelWidth(object value)
    {
        Value = value;
    }

    internal object Value { get; }

    /// <summary>
    /// Creates a width in device-independent pixels.
    /// </summary>
    /// <param name="value">A finite, non-negative width no greater than <see cref="float.MaxValue"/>.</param>
    /// <returns>The width hint.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside the supported range.</exception>
    public static DockLabelWidth Dips(double value)
    {
        Validate(value);
        return new(value);
    }

    /// <summary>
    /// Creates a width in multiples of the zero glyph in the target row's font and text scale.
    /// Shared limits use the title font.
    /// </summary>
    /// <param name="value">A finite, non-negative count no greater than <see cref="float.MaxValue"/>.</param>
    /// <returns>The width hint.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside the supported range.</exception>
    public static DockLabelWidth Characters(double value)
    {
        Validate(value);
        return new((value == 0 ? 0 : value).ToString("R", CultureInfo.InvariantCulture) + "ch");
    }

    /// <summary>
    /// Creates a width by measuring literal text in the target row's font and text scale.
    /// Shared limits use the title font. The sample is independent of the displayed text.
    /// </summary>
    /// <param name="text">The text to measure; an empty string reserves zero width.</param>
    /// <returns>The width hint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static DockLabelWidth Sample(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new("text:" + text);
    }

    private static void Validate(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > float.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The width must be finite and between zero and Single.MaxValue.");
        }
    }
}
