// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PowerToys.Settings.DSC.Schema;

internal sealed class Common
{
    private static string[] TypeParts(string name)
    {
        return Regex.Split(name.ToLower(CultureInfo.CurrentCulture), @"(?<!^)(?=[A-Z])|\.");
    }

    internal static bool InferIsBool(Type propertyInfo)
    {
        return TypeParts(propertyInfo.Name).Any(word => word.Equals("Bool", StringComparison.OrdinalIgnoreCase) || word.Equals("Boolean", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool InferIsInt(Type propertyInfo)
    {
        return TypeParts(propertyInfo.Name).Any(word => word.Contains("Int", StringComparison.OrdinalIgnoreCase));
    }
}
