// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Collections.Generic;

    public static class ReadableStringHelper
    {
        private const int DecimalPercision = 10;

        public static string BytesToReadableString(int bytes)
        {
            // TODO: get string from resources
            List<string> format = new List<string>
            {
                "B",
                "KB",
                "MB",
                "GB",
                "TB",
                "PB",
                "EB",
            };

            int index = 0;
            double number = 0.0;

            if (bytes > 0)
            {
                index = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
                number = Math.Round((bytes / Math.Pow(1024, index)) * DecimalPercision) / DecimalPercision;
            }

            return string.Concat(number, format[index]);
        }
    }
}
