// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public interface IQuickAccessLauncher
    {
        void Launch(ModuleType moduleType);
    }
}
