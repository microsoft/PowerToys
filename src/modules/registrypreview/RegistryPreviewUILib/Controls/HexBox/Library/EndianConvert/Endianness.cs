// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <history>
//     2020-... created by Filip Jeremic (fjeremic) as "HexView.Wpf".
//     2024-... republished by @hotkidfamily as "HexBox.WinUI".
//     2025 Included in PowerToys. (Branch master; commit 72dcf64dc858c693a7a16887004c8ddbab61fce7.)
// </history>

namespace RegistryPreviewUILib.HexBox.Library.EndianConvert
{
    /// <summary>
    /// Represents the endianness of a value in a computer architecture.
    /// </summary>
    public enum Endianness
    {
        /// <summary>
        /// Most significant byte first.
        /// </summary>
        BigEndian,

        /// <summary>
        /// Least significant byte first.
        /// </summary>
        LittleEndian,
    }
}
