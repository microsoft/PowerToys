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
    /// Enumerates how the data (bytes read from the buffer) is to be interpreted when displayed.
    /// </summary>
    public enum DataType
    {
        /// <summary>
        /// Display the data as integral (integer) values.
        /// </summary>
        Int_1 = 1,
        /// <summary>
        /// Display the data as integral (integer) values.
        /// </summary>
        Int_2 = 2,
        /// <summary>
        /// Display the data as integral (integer) values.
        /// </summary>
        Int_4 = 4,
        /// <summary>
        /// Display the data as integral (integer) values.
        /// </summary>
        Int_8 = 8,
        /// <summary>
        /// Display the data as floating point values.
        /// </summary>
        Float_32 = 32,
        /// <summary>
        /// Display the data as floating point values.
        /// </summary>
        Float_64 = 64,
    }
}
