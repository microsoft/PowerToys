// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace FileActionsMenu.Ui.Helpers
{
    internal static class Extensions
    {
        public static T GetOrArgumentNullException<T>(this T? value)
        {
            return value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
