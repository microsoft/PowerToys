// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public interface IQuickAccessLauncher
    {
        bool Launch(ModuleType moduleType);

        // Some modules put more than one entry in the flyout. The action says which of
        // them was pressed; null means the module's single default entry.
        bool Launch(ModuleType moduleType, string? action);
    }
}
