// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class InputSimulationTests
{
    [TestMethod]
    [DataRow(0, 0)]
    [DataRow((int)WM.LLKHF.UP, (int)NativeMethods.KEYEVENTF.KEYUP)]
    [DataRow((int)WM.LLKHF.EXTENDED, (int)NativeMethods.KEYEVENTF.EXTENDEDKEY)]
    [DataRow((int)(WM.LLKHF.UP | WM.LLKHF.EXTENDED), (int)(NativeMethods.KEYEVENTF.KEYUP | NativeMethods.KEYEVENTF.EXTENDEDKEY))]
    public void RemoteKeyboardBypassTagsInputWithoutChangingKeyOrFlags(int hookFlags, int inputFlags)
    {
        var key = new KEYBDDATA { wVk = (int)VK.RCONTROL, dwFlags = hookFlags };
        var input = InputSimulation.CreateKeyboardInput(key, bypassKeyboardManager: true);

        Assert.AreEqual(new IntPtr(0x101), input.ki.dwExtraInfo);
        Assert.AreEqual(inputFlags, input.ki.dwFlags);
        Assert.AreEqual((short)key.wVk, input.ki.wVk);
        Assert.AreEqual((short)NativeMethods.MapVirtualKey((uint)key.wVk, 0), input.ki.wScan);
    }

    [TestMethod]
    public void DisabledBypassAndLocalInputPreserveMessageExtraInfo()
    {
        var key = new KEYBDDATA { wVk = (int)VK.SNAPSHOT };
        var extraInfo = NativeMethods.GetMessageExtraInfo();

        var remote = InputSimulation.CreateKeyboardInput(key, bypassKeyboardManager: false);
        var local = InputSimulation.CreateKeyboardInput(key);

        Assert.AreEqual(extraInfo, remote.ki.dwExtraInfo);
        Assert.AreEqual(extraInfo, local.ki.dwExtraInfo);
        Assert.AreEqual(remote.ki.wVk, local.ki.wVk);
        Assert.AreEqual(remote.ki.dwFlags, local.ki.dwFlags);
    }

    [TestMethod]
    public void RemoteKeyboardBypassPolicyRemainsStableWhileKeyIsHeld()
    {
        var keyDown = new KEYBDDATA { wVk = (int)VK.RCONTROL };
        var keyUp = new KEYBDDATA { wVk = keyDown.wVk, dwFlags = (int)WM.LLKHF.UP };

        Assert.IsFalse(InputSimulation.GetRemoteKeyboardBypassPolicy(keyDown, bypassKeyboardManager: false));
        Assert.IsFalse(InputSimulation.GetRemoteKeyboardBypassPolicy(keyDown, bypassKeyboardManager: true));
        Assert.IsFalse(InputSimulation.GetRemoteKeyboardBypassPolicy(keyUp, bypassKeyboardManager: true));

        Assert.IsTrue(InputSimulation.GetRemoteKeyboardBypassPolicy(keyDown, bypassKeyboardManager: true));
        Assert.IsTrue(InputSimulation.GetRemoteKeyboardBypassPolicy(keyUp, bypassKeyboardManager: false));
    }

    [TestMethod]
    public void CleanupReleaseUsesTrackedRemoteBypassPolicy()
    {
        var keyDown = new KEYBDDATA { wVk = (int)VK.RMENU };

        Assert.IsTrue(InputSimulation.GetRemoteKeyboardBypassPolicy(keyDown, bypassKeyboardManager: true));
        Assert.IsTrue(InputSimulation.ConsumeRemoteKeyboardBypassPolicy(keyDown.wVk));
        Assert.IsFalse(InputSimulation.ConsumeRemoteKeyboardBypassPolicy(keyDown.wVk));
    }
}
