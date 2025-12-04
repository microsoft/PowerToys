// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.PowerToys.QuickAccess.Services;

public interface IQuickAccessCoordinator
{
    bool IsRunnerElevated { get; }

    void HideFlyout();

    void OpenSettings();

    void OpenSettingsForModule(ModuleType moduleType);

    void OpenGeneralSettingsForUpdates();

    Task<bool> ShowDocumentationAsync();

    void NotifyUserSettingsInteraction();

    bool UpdateModuleEnabled(ModuleType moduleType, bool isEnabled);

    void ReportBug();

    void OnModuleLaunched(ModuleType moduleType);
}
