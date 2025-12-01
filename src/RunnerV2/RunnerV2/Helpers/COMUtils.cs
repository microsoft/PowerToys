// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RunnerV2.Helpers
{
    internal static class COMUtils
    {
        public static void InitializeCOMSecurity(string securityDescriptor)
        {
            if (!NativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptorW(
                securityDescriptor,
                1,
                out IntPtr pSD,
                out _))
            {
                return;
            }

            uint absoluteSDSize = 0;
            uint daclSize = 0;
            uint groupSize = 0;
            uint ownerSize = 0;
            uint saclSize = 0;

            if (!NativeMethods.MakeAbsoluteSD(pSD, IntPtr.Zero, ref absoluteSDSize, IntPtr.Zero, ref daclSize, IntPtr.Zero, ref saclSize, IntPtr.Zero, ref ownerSize, IntPtr.Zero, ref groupSize))
            {
                return;
            }

            IntPtr absoluteSD = Marshal.AllocHGlobal((int)absoluteSDSize);
            IntPtr dacl = Marshal.AllocHGlobal((int)daclSize);
            IntPtr sacl = Marshal.AllocHGlobal((int)saclSize);
            IntPtr owner = Marshal.AllocHGlobal((int)ownerSize);
            IntPtr group = Marshal.AllocHGlobal((int)groupSize);

            if (!NativeMethods.MakeAbsoluteSD(pSD, absoluteSD, ref absoluteSDSize, dacl, ref daclSize, sacl, ref saclSize, owner, ref ownerSize, group, ref groupSize))
            {
                return;
            }

            _ = NativeMethods.CoInitializeSecurity(
                absoluteSD,
                -1,
                IntPtr.Zero,
                IntPtr.Zero,
                6, // RPC_C_AUTHN_LEVEL_PKT_PRIVACY
                2, // RPC_C_IMP_LEVEL_IDENTIFY
                IntPtr.Zero,
                64 | 4096, // EOAC_DYNAMIC_CLOAKING | EOAC_DISABLE_AAA
                IntPtr.Zero);
        }
    }
}
