/*// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.Settings.UI.Lib.Utilities
{
    public class MutexExtension
    {
        [DllImport("kernel32", EntryPoint = "OpenMutexW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeWaitHandle OpenMutex(uint desiredAccess, bool inheritHandle, string name);

        public static Mutex TryOpenExisting(string name, MutexRights rights, out Mutex result)
        {
            SafeWaitHandle myHandle = OpenMutex((uint)rights, false, name);

            if (myHandle.IsInvalid)
            {
                return false;
            }

            result = new Mutex(initiallyOwned: false);
            SafeWaitHandle old = result.SafeWaitHandle;
            result.SafeWaitHandle = handle;
            old.Dispose();

            return true;
        }
    }
}
*/