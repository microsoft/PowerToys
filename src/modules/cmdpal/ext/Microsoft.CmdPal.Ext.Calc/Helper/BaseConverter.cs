// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Text;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static class BaseConverter
{
    private const string Digits = "0123456789ABCDEF";

    public static string Convert(BigInteger value, int toBase)
    {
        var prefix = toBase switch
        {
            2 => "0b",
            8 => "0o",
            16 => "0x",
            _ => string.Empty,
        };

        if (toBase is < 2 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(toBase), "Base must be between 2 and 16.");
        }

        if (value == BigInteger.Zero)
        {
            return prefix + "0";
        }

        var abs = BigInteger.Abs(value);
        var sb = new StringBuilder();

        while (abs > 0)
        {
            var digit = (int)(abs % toBase);
            sb.Insert(0, Digits[digit]);
            abs /= toBase;
        }

        var sign = value < 0 ? "-" : string.Empty;

        return sign + prefix + sb;
    }
}
