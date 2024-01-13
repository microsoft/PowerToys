// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;
using ManagedCommon;
using Peek.Common.Models;
using Peek.UI.Native;

namespace Peek.UI.Helpers
{
    public static class DefaultAppHelper
    {
        public static string TryGetDefaultAppName(string extension)
        {
            string appName = string.Empty;

            // Get the length of the app name
            uint length = 0;
            HResult ret = NativeMethods.AssocQueryString(NativeMethods.AssocF.Verify, NativeMethods.AssocStr.FriendlyAppName, extension, null, null, ref length);
            if (ret != HResult.False)
            {
                Logger.LogError($"Error when getting accessString for {extension} file: {Marshal.GetExceptionForHR((int)ret)!.Message}");
                return appName;
            }

            // Get the app name
            StringBuilder sb = new((int)length);
            ret = NativeMethods.AssocQueryString(NativeMethods.AssocF.Verify, NativeMethods.AssocStr.FriendlyAppName, extension, null, sb, ref length);
            if (ret != HResult.Ok)
            {
                Logger.LogError($"Error when getting accessString for {extension} file: {Marshal.GetExceptionForHR((int)ret)!.Message}");
                return appName;
            }

            appName = sb.ToString();
            return appName;
        }
    }
}
