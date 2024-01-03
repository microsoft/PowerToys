// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.ObjectPool;

namespace WinGetCommandNotFound
{
    public sealed class PooledPowerShellObjectPolicy : IPooledObjectPolicy<PowerShell>
    {
        private static readonly string[] WingetClientModuleName = new[] { "Microsoft.WinGet.Client" };

        public PowerShell Create()
        {
            var iss = InitialSessionState.CreateDefault2();
            iss.ImportPSModule(WingetClientModuleName);
            return PowerShell.Create(iss);
        }

        public bool Return(PowerShell obj)
        {
            if (obj != null)
            {
                obj.Commands.Clear();
                obj.Streams.ClearStreams();
                return true;
            }

            return false;
        }
    }
}
