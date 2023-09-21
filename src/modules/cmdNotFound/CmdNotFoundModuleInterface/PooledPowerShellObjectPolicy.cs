// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Management.Automation;
using Microsoft.Extensions.ObjectPool;

namespace WinGetCommandNotFound
{
    public sealed class PooledPowerShellObjectPolicy : PooledObjectPolicy<PowerShell>
    {
        public override PowerShell Create()
        {
            var iss = System.Management.Automation.Runspaces.InitialSessionState.CreateDefault2();
            iss.ImportPSModule(new[] { "Microsoft.WinGet.Client" });
            return PowerShell.Create(iss);
        }

        public override bool Return(PowerShell ps)
        {
            if (ps != null)
            {
                ps.Commands.Clear();
                ps.Streams.ClearStreams();
            }

            return true;
        }
    }
}
