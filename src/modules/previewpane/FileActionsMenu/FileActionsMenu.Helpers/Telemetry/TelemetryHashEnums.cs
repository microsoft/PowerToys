// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileActionsMenu.Helpers.Telemetry
{
    public static class TelemetryHashEnums
    {
        public enum HashGenerateOrVerifyType
        {
            InFilename,
            SingleFile,
            MuultipleFiles,
            Clipboard,
        }

        public enum HashType
        {
            CRC32DECIMAL,
            CRC32HEX,
            CRC64DECIMAL,
            CRC64HEX,
            MD5,
            SHA1,
            SHA256,
            SHA384,
            SHA512,
            SHA3_256,
            SHA3_384,
            SHA3_512,
        }
    }
}
