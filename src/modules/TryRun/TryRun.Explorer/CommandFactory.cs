// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerToys.TryRun.Explorer;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class CommandFactory(ExplorerCommand command) : ShellInterop.IClassFactory
{
    public int CreateInstance(IntPtr outer, ref Guid interfaceId, out IntPtr instance)
    {
        instance = IntPtr.Zero;
        return outer != IntPtr.Zero ? unchecked((int)0x80040110) : ShellInterop.QueryInterface(command, ref interfaceId, out instance);
    }

    public int LockServer(bool locked) => 0;
}
