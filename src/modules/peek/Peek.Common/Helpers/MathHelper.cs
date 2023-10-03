// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Peek.Common.Helpers
{
    public static class MathHelper
    {
        public static int Modulo(int a, int b)
        {
            return a < 0 ? ((a % b) + b) % b : a % b;
        }

        public static int NumberOfDigits(int num)
        {
            return Math.Abs(num).ToString(CultureInfo.InvariantCulture).Length;
        }
    }
}
