// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using Peek.UI.Native;

namespace Peek.UI.Helpers
{
    public static class DefaultAppHelper
    {
        public static string? TryGetDefaultAppName(string extension)
        {
            const int S_OK = 0, S_FALSE = 1;
            string? appName = null;

            // Get the length of the app name
            uint length = 0;
            uint ret = NativeMethods.AssocQueryString(NativeMethods.AssocF.Verify, NativeMethods.AssocStr.FriendlyAppName, extension, null, null, ref length);
            if (ret != S_FALSE)
            {
                Debug.WriteLine("Error when getting accessString for {2} file", extension);
                return appName;
            }

            // Get the the app name
            var sb = new StringBuilder((int)length);
            ret = NativeMethods.AssocQueryString(NativeMethods.AssocF.Verify, NativeMethods.AssocStr.FriendlyAppName, extension, null, sb, ref length);
            if (ret != S_OK)
            {
                Debug.WriteLine("Error when getting accessString for {2} file", extension);
                return appName;
            }

            appName = sb.ToString();
            return appName;
        }
    }
}
