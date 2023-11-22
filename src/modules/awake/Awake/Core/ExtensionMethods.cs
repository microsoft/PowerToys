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

            foreach (var element in source)
            {
                target.Add(element);
            }
        }
    }
}
