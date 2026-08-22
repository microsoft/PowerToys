// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.IO;
class P {
  [DllImport("advapi32",SetLastError=true)] static extern bool SaferCreateLevel(int s,int l,int o,out IntPtr h,IntPtr r);
  [DllImport("advapi32",SetLastError=true)] static extern bool SaferComputeTokenFromLevel(IntPtr h,IntPtr it,out IntPtr ot,int f,IntPtr r);
  [DllImport("advapi32",SetLastError=true)] static extern bool SaferCloseLevel(IntPtr h);
  [DllImport("advapi32",SetLastError=true)] static extern bool ImpersonateLoggedOnUser(IntPtr t);
  [DllImport("advapi32",SetLastError=true)] static extern bool RevertToSelf();
  static int Main(string[] a){
    string f=a[0]; IntPtr lvl,tok;
    SaferCreateLevel(2,0x20000,1,out lvl,IntPtr.Zero);
    SaferComputeTokenFromLevel(lvl,IntPtr.Zero,out tok,0,IntPtr.Zero);
    SaferCloseLevel(lvl);
    ImpersonateLoggedOnUser(tok);
    Console.WriteLine("[as] "+System.Security.Principal.WindowsIdentity.GetCurrent().Name+" (non-elevated SAFER token)");
    try { File.WriteAllText(f,"PWNED"); Console.WriteLine("WRITE : SUCCEEDED  <-- lock broken"); }
    catch(Exception e){ Console.WriteLine("WRITE : rejected -> "+e.GetType().Name); }
    try { File.Delete(f); Console.WriteLine("DELETE: SUCCEEDED  <-- lock broken"); }
    catch(Exception e){ Console.WriteLine("DELETE: rejected -> "+e.GetType().Name); }
    RevertToSelf(); return 0;
  }
}
