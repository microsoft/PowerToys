// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.Win32;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class RegistryPreviewModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleCustomActionsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public bool Enabled => SettingsUtils.Default.GetSettingsOrDefault<GeneralSettings>().Enabled.RegistryPreview;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();

        public string Name => "RegistryPreview";

        public override string ProcessPath => "WinUI3Apps\\PowerToys.RegistryPreview.exe";

        public override string ProcessName => "PowerToys.RegistryPreview";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SupressLaunchOnModuleEnabled | ProcessLaunchOptions.NeverExit;

        public void Disable()
        {
            if (!RegistryPreviewChangeSet.UnApplyIfApplied())
            {
                Logger.LogError("Unapplying registry changes failed");
            }
        }

        public void OnSettingsChanged()
        {
            bool defaultRegApp = SettingsUtils.Default.GetSettings<RegistryPreviewSettings>(Name).Properties.DefaultRegApp;
            if (defaultRegApp && !RegistryPreviewSetDefaultAppChangeSet.IsApplied)
            {
                if (!RegistryPreviewSetDefaultAppChangeSet.Apply())
                {
                    Logger.LogError("Applying reg default handler failed.");
                }

                NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            else if (!defaultRegApp && RegistryPreviewSetDefaultAppChangeSet.IsApplied)
            {
                if (RegistryPreviewSetDefaultAppChangeSet.UnApply())
                {
                    Logger.LogError("Unapplying reg default handler failed.");
                }

                NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public void Enable()
        {
            if (!RegistryPreviewChangeSet.ApplyIfNotApplied())
            {
                Logger.LogError("Applying registry changes failed");
            }

            OnSettingsChanged();
        }

        public Dictionary<string, Action> CustomActions
        {
            get => new() { { "Launch", () => LaunchProcess() } };
        }

        private RegistryChangeSet RegistryPreviewSetDefaultAppChangeSet
        {
            get
            {
                string installationDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

                string appName = "Registry Preview";
                string registryKeyPrefix = "Software\\Classes\\";
                RegistryValueChange[] changes =
                    [
                        new RegistryValueChange
                    {
                        KeyPath = registryKeyPrefix + ProcessName + "\\Application",
                        KeyName = "ApplicationName",
                        Value = appName,
                    },
                    new RegistryValueChange
                    {
                        KeyPath = registryKeyPrefix + ProcessName + "\\DefaultIcon",
                        KeyName = null,
                        Value = installationDir + "\\WinUI3Apps\\PowerToys.RegistryPreview.exe",
                    },
                    new RegistryValueChange
                    {
                        KeyPath = registryKeyPrefix + ProcessName + "\\shell\\open\\command",
                        KeyName = null,
                        Value = installationDir + "\\WinUI3Apps\\PowerToys.RegistryPreview.exe \"----ms-protocol:ms-encodedlaunch:App?ContractId=Windows.File&Verb=open&File=%1\"",
                    },
                    new RegistryValueChange
                    {
                        KeyPath = registryKeyPrefix + ".reg\\OpenWithProgIDs",
                        KeyName = null,
                        Value = ProcessName,
                    }
                    ];
                return new RegistryChangeSet { Changes = changes };
            }
        }

        private RegistryChangeSet RegistryPreviewChangeSet
        {
            get
            {
                string installationDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

                RegistryValueChange[] changes =
                    [
                        new RegistryValueChange
                    {
                        KeyPath = "Software\\Classes\\regfile\\shell\\preview\\command",
                        KeyName = null,
                        Value = installationDir + "\\WinUI3Apps\\PowerToys.RegistryPreview.exe \"%1\"",
                    },
                    new RegistryValueChange
                    {
                        KeyPath = "Software\\Classes\\regfile\\shell\\preview",
                        KeyName = "icon",
                        Value = installationDir + "\\WinUI3Apps\\Assets\\RegistryPreview\\RegistryPreview.ico",
                    }
                    ];
                return new RegistryChangeSet() { Changes = changes };
            }
        }
    }
}
