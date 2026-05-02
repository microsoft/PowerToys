// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class HotkeyManagerTests
{
    [TestMethod]
    public void HotkeyManager_Dispose_DoesNotThrow()
    {
        using var mgr = new HotkeyManager();

        // Dispose before Start should be safe (no STA thread running)
    }
}
