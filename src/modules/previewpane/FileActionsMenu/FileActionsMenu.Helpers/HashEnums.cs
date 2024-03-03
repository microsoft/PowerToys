// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileActionsMenu.Helpers
{
    public static class HashEnums
    {
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

        public enum GenerateOrVerifyMode
        {
            SingleFile,
            MultipleFiles,
            Filename,
            Clipboard,
        }
    }
}
