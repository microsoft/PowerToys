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

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public static class TimeSpanHelper
{
    public static string Convert(TimeSpan? time)
    {
        if (time is not TimeSpan ts)
        {
            return string.Empty;
        }

        // If user passed in a negative TimeSpan, normalize
        if (ts < TimeSpan.Zero)
        {
            ts = ts.Duration();
        }

        // Map the TimeSpan to a DateTime on today's date
        var dt = DateTime.Today.Add(ts);

        // This pattern automatically respects system 12/24-hour setting
        string pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;

        return dt.ToString(pattern, CultureInfo.CurrentCulture);
    }
}
