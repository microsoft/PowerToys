// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <history>
//     2020-... created by Filip Jeremic (fjeremic) as "HexView.Wpf".
//     2024-... republished by @hotkidfamily as "HexBox.WinUI".
//     2025 Included in PowerToys. (Branch master; commit 72dcf64dc858c693a7a16887004c8ddbab61fce7.)
// </history>

namespace RegistryPreviewUILib.HexBox
{
    using System;

    /// <summary>
    /// A utility class with miscellaneous methods.
    /// </summary>
    internal static class Utilities
    {
        /// <summary>
        /// Clamps the <paramref name="value"/> to the range [<paramref name="min"/>, <paramref name="max"/>].
        /// </summary>
        ///
        /// <typeparam name="T">
        /// The type of the value to clamp.
        /// </typeparam>
        ///
        /// <param name="value">
        /// The value to clamp.
        /// </param>
        ///
        /// <param name="min">
        /// The upper bound on the clamped value.
        /// </param>
        ///
        /// <param name="max">
        /// The lower bound on the clmaped value.
        /// </param>
        ///
        /// <returns>
        /// The nearest value of <paramref name="value"/> in the range [<paramref name="min"/>,
        /// <paramref name="max"/>].
        /// </returns>
        public static T Clamp<T>(this T value, T min, T max)
            where T : IComparable<T>
        {
            return value.CompareTo(min) < 0 ? min : value.CompareTo(max) > 0 ? max : value;
        }

        /// <summary>
        /// Calculates the arithmetic modulus of <paramref name="n"/> modulo <paramref name="m"/>.
        /// </summary>
        ///
        /// <typeparam name="T">
        /// The type of the values.
        /// </typeparam>
        ///
        /// <param name="n">
        /// The value to compute the modulus of.
        /// </param>
        ///
        /// <param name="m">
        /// The modulus.
        /// </param>
        ///
        /// <returns>
        /// The non-negative value <c>r</c> such that for some integral value <c>q</c>:
        /// <c><paramref name="n"/> = q*m + r</c>.
        /// </returns>
        public static T Mod<T>(this T n, T m)
            where T : IComparable<T>
        {
            dynamic dn = n;
            dynamic dm = m;

            dynamic dr = dn % dm;

            return dr.CompareTo(0) < 0 ? dr + dm : dr;
        }
    }
}
