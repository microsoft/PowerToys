// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library.Interfaces
{
    public interface IHotkeyConfig
    {
        HotkeyAccessor[] GetAllHotkeyAccessors();

        ModuleType GetModuleType();
    }
}
