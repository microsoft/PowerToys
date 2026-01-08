// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.Interop;

namespace Microsoft.PowerToys.QuickAccess.Services
{
    public class QuickAccessLauncher : Microsoft.PowerToys.Settings.UI.Controls.QuickAccessLauncher
    {
        private readonly IQuickAccessCoordinator? _coordinator;

        public QuickAccessLauncher(IQuickAccessCoordinator? coordinator)
            : base(coordinator?.IsRunnerElevated ?? false)
        {
            _coordinator = coordinator;
        }

        public override bool Launch(ModuleType moduleType)
        {
            bool moduleRun = base.Launch(moduleType);

            if (moduleRun)
            {
                _coordinator?.OnModuleLaunched(moduleType);
            }

            _coordinator?.HideFlyout();

            return moduleRun;
        }
    }
}
