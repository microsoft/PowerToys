// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Peek.Common.Converters
{
    public static class VisibilityConverter
    {
        public static Visibility Convert(bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public static Visibility Invert(bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
