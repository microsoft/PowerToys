// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Versioning;
using Awake.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Awake.UnitTests;

[SupportedOSPlatform("windows")]
[TestClass]
public class SessionStateControllerTests
{
    [TestMethod]
    public void InitializeLockState_UsesProvidedCurrentState()
    {
        bool locked = SessionStateController.InitializeLockState(() => true);
        Assert.IsTrue(locked);

        locked = SessionStateController.InitializeLockState(() => false);
        Assert.IsFalse(locked);
    }

    [TestMethod]
    public void ApplySessionSwitch_WhenSessionLockEventOccurs_UpdatesStateToLocked()
    {
        bool locked = SessionStateController.ApplySessionSwitch(SessionSwitchReason.SessionLock, currentState: false);

        Assert.IsTrue(locked);
    }

    [TestMethod]
    public void ApplySessionSwitch_WhenSessionUnlockEventOccurs_UpdatesStateToUnlocked()
    {
        bool locked = SessionStateController.ApplySessionSwitch(SessionSwitchReason.SessionUnlock, currentState: true);

        Assert.IsFalse(locked);
    }

    [TestMethod]
    public void ApplySessionSwitch_WhenOtherSessionEventOccurs_PreservesCurrentState()
    {
        bool locked = SessionStateController.ApplySessionSwitch((SessionSwitchReason)999, currentState: true);

        Assert.IsTrue(locked);

        locked = SessionStateController.ApplySessionSwitch((SessionSwitchReason)999, currentState: false);

        Assert.IsFalse(locked);
    }
}
