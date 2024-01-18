// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Windows;

namespace FancyZonesEditor.Controls
{
    internal static class SharedHelpers
    {
        public static Window GetActiveWindow()
        {
            return Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
        }
    }
}
