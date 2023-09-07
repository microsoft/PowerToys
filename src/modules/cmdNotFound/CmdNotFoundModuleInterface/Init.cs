// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Feedback;

namespace WinGetCommandNotFound
{
    public sealed class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        internal const string Id = "e5351aa4-dfde-4d4d-bf0f-1a2f5a37d8d6";

        public void OnImport()
        {
            if (!Platform.IsWindows)
            {
                return;
            }

            // Ensure WinGet is installed
            using (var pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                var results = pwsh.AddCommand("Get-Command")
                    .AddParameter("Name", "winget")
                    .AddParameter("CommandType", "Application")
                    .Invoke();

                if (results.Count is 0)
                {
                    return;
                }
            }

            SubsystemManager.RegisterSubsystem<IFeedbackProvider, WinGetCommandNotFoundFeedbackPredictor>(WinGetCommandNotFoundFeedbackPredictor.Singleton);
        }

        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            SubsystemManager.UnregisterSubsystem<IFeedbackProvider>(new Guid(Id));
        }
    }
}
