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
    /// <summary>
    /// Enumerates the address column formatting options.
    /// </summary>
    public enum AddressFormat
    {
        /// <summary>
        /// 16 bit HEX address "0000".
        /// </summary>
        Address16,

        /// <summary>
        /// 24 bit HEX address "00:0000".
        /// </summary>
        Address24,

        /// <summary>
        /// 32 bit HEX address "0000:0000".
        /// </summary>
        Address32,

        /// <summary>
        /// 48 bit HEX address "0000:00000000".
        /// </summary>
        Address48,

        /// <summary>
        /// 64 bit HEX address "00000000:00000000".
        /// </summary>
        Address64,
    }
}
