// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal static class PowerScriptFormatting
{
    private static readonly ConcurrentDictionary<string, CompositeFormat> Formats = new(StringComparer.Ordinal);

    internal static string Format<T>(string format, T value) =>
        string.Format(CultureInfo.CurrentCulture, Formats.GetOrAdd(format, static text => CompositeFormat.Parse(text)), value);

    internal static string Format<TFirst, TSecond>(string format, TFirst first, TSecond second) =>
        string.Format(CultureInfo.CurrentCulture, Formats.GetOrAdd(format, static text => CompositeFormat.Parse(text)), first, second);
}
