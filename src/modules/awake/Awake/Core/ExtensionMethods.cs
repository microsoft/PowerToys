// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Awake.Core
{
    internal static class ExtensionMethods
    {
        public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);

            foreach (T? element in source)
            {
                target.Add(element);
            }
        }

        public static string ToHumanReadableString(this TimeSpan timeSpan)
        {
            // Get days, hours, minutes, and seconds from the TimeSpan
            int days = timeSpan.Days;
            int hours = timeSpan.Hours;
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;

            // Format the string based on the presence of days, hours, minutes, and seconds
            return $"{days:D2}{Properties.Resources.AWAKE_LABEL_DAYS} {hours:D2}{Properties.Resources.AWAKE_LABEL_HOURS} {minutes:D2}{Properties.Resources.AWAKE_LABEL_MINUTES} {seconds:D2}{Properties.Resources.AWAKE_LABEL_SECONDS}";
        }
    }
}
