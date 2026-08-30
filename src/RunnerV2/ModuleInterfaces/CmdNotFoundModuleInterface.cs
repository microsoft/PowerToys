// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ManagedCommon;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class CmdNotFoundModuleInterface : IPowerToysModule
    {
        public string Name => "CmdNotFound";

        public bool Enabled => true;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredCmdNotFoundEnabledValue();

        public CmdNotFoundModuleInterface()
        {
            if (GpoRuleConfigured == GpoRuleConfigured.Disabled)
            {
                UninstallModule();
            }

            if (GpoRuleConfigured == GpoRuleConfigured.Enabled)
            {
                InstallModule();
            }
        }

        public void InstallModule()
        {
            Logger.LogInfo("Installing Command Not Found module invoked through GPO");

            new Thread(async () =>
            {
                Process p = Process.Start("pwsh.exe", "-NoProfile -NonInteractive -NoLogo -WindowStyle Hidden -ExecutionPolicy Unrestricted -File \"" + Path.GetDirectoryName(Environment.ProcessPath) + "\\WinUI3Apps\\Assets\\Settings\\Scripts\\EnableModule.ps1" + "\"" + " -scriptPath \"" + Path.GetDirectoryName(Environment.ProcessPath) + "\"");
                await p.WaitForExitAsync();
                if (p.ExitCode == 0)
                {
                    Logger.LogInfo("Command Not Found was successfully installed.");
                    return;
                }

                Logger.LogInfo("Command Not Found failed to install with exit code: " + p.ExitCode);
            }).Start();
        }

        public void UninstallModule()
        {
            Logger.LogInfo("Uninstalling Command Not Found module invoked through GPO");

            new Thread(async () =>
            {
                Process p = Process.Start("pwsh.exe", "-NoProfile -NonInteractive -NoLogo -WindowStyle Hidden -ExecutionPolicy Unrestricted -File \"" + Path.GetDirectoryName(Environment.ProcessPath) + "\\WinUI3Apps\\Assets\\Settings\\Scripts\\DisableModule.ps1" + "\"" + " -scriptPath \"" + Path.GetDirectoryName(Environment.ProcessPath) + "\"");
                await p.WaitForExitAsync();
                if (p.ExitCode == 0)
                {
                    Logger.LogInfo("Command Not Found was successfully uninstalled.");
                    return;
                }

                Logger.LogInfo("Command Not Found failed to uninstall with exit code: " + p.ExitCode);
            }).Start();
        }

        public void Disable()
        {
        }

        public void Enable()
        {
        }
    }
}
