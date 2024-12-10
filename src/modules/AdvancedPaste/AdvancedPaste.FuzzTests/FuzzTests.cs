// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.FuzzTests
{
    public class FuzzTests
    {
        public static void ValidatePhoneNumber(string someString)
        {
            if (someString.Length < 10)
            {
                return;
            }
        }

        public static void FuzzPhoneNumber(ReadOnlySpan<byte> input)
        {
            ValidatePhoneNumber(input.ToString());
        }
    }
}
