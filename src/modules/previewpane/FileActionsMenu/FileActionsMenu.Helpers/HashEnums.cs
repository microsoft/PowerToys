// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileActionsMenu.Helpers
{
    /// <summary>
    /// The enums for hash types and generate or verify mode.
    /// </summary>
    /// <remarks>
    /// New values shall be added to the end of the enum to avoid breaking changes.
    /// </remarks>
    public static class HashEnums
    {
        /// <summary>
        /// Hash type
        /// </summary>
        public enum HashType
        {
            MD5,
            SHA1,
            SHA256,
            SHA384,
            SHA512,
            SHA3_256,
            SHA3_384,
            SHA3_512,
            CRC32Hex,
            CRC32Decimal,
            CRC64Hex,
            CRC64Decimal,
        }

        /// <summary>
        /// Generate or verify mode
        /// </summary>
        public enum GenerateOrVerifyMode
        {
            /// <summary>
            /// Hashes are saved in a file called hashes.
            /// </summary>
            SingleFile,

            /// <summary>
            /// Hashes are saved in multiple files with the same name as the original file.
            /// </summary>
            MultipleFiles,

            /// <summary>
            /// Hash is in the filename.
            /// </summary>
            Filename,

            /// <summary>
            /// Hash is in the clipboard.
            /// </summary>
            Clipboard,
        }
    }
}
