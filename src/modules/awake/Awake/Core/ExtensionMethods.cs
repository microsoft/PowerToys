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
            // Format as H:MM:SS or M:SS depending on total hours
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }

            return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
        }
    }
}
