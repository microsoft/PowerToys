// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

using AdvancedPaste.Core;

namespace AdvancedPaste.FuzzTests
{
    public class FuzzTests
    {
        public static void FuzzToJsonFromXmlOrCsv(ReadOnlySpan<byte> input)
        {
            string text = Encoding.UTF8.GetString(input);
            _ = JsonConverter.Convert(text);
        }
    }
}
