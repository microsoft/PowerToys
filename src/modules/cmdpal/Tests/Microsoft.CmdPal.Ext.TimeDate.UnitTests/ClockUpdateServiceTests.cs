// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TimeDate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class ClockUpdateServiceTests
{
    private static readonly DateTime InitialTime = new(2025, 7, 1, 14, 5, 20);

    [TestMethod]
    public void DispatchTick_OnlyInvokesMinuteClientsWhenMinuteChanges()
    {
        using var service = new ClockUpdateService(() => InitialTime, enableTimer: false);
        var minuteClient = new object();
        var secondClient = new object();
        var minuteUpdates = 0;
        var secondUpdates = 0;
        service.Subscribe(minuteClient, (_, _) => minuteUpdates++, requiresSecondUpdates: false);
        service.Subscribe(secondClient, (_, _) => secondUpdates++, requiresSecondUpdates: true);

        service.DispatchTick(InitialTime.AddSeconds(1));
        service.DispatchTick(InitialTime.AddSeconds(2));

        Assert.AreEqual(0, minuteUpdates);
        Assert.AreEqual(2, secondUpdates);

        service.DispatchTick(InitialTime.AddMinutes(1));

        Assert.AreEqual(1, minuteUpdates);
        Assert.AreEqual(3, secondUpdates);
    }

    [TestMethod]
    public void SetRequiresSecondUpdates_MovesClientToNewCadence()
    {
        using var service = new ClockUpdateService(() => InitialTime, enableTimer: false);
        var client = new object();
        var updates = 0;
        service.Subscribe(client, (_, _) => updates++, requiresSecondUpdates: false);

        service.SetRequiresSecondUpdates(client, true);
        service.DispatchTick(InitialTime.AddSeconds(1));
        service.SetRequiresSecondUpdates(client, false);
        service.DispatchTick(InitialTime.AddSeconds(2));
        service.DispatchTick(InitialTime.AddMinutes(1));

        Assert.AreEqual(2, updates);
    }

    [TestMethod]
    public void Unsubscribe_StopsClientUpdates()
    {
        using var service = new ClockUpdateService(() => InitialTime, enableTimer: false);
        var client = new object();
        var updates = 0;
        service.Subscribe(client, (_, _) => updates++, requiresSecondUpdates: true);

        service.Unsubscribe(client);
        service.DispatchTick(InitialTime.AddSeconds(1));

        Assert.AreEqual(0, updates);
    }

    [TestMethod]
    public void DispatchTick_TimeJumpInvokesMinuteClientOnce()
    {
        using var service = new ClockUpdateService(() => InitialTime, enableTimer: false);
        var client = new object();
        var updates = 0;
        service.Subscribe(client, (_, _) => updates++, requiresSecondUpdates: false);

        service.DispatchTick(InitialTime.AddHours(2));
        service.DispatchTick(InitialTime.AddHours(2).AddSeconds(1));
        service.DispatchTick(InitialTime.AddHours(-2));

        Assert.AreEqual(2, updates);
    }

    [TestMethod]
    public void DispatchTick_HandlerFailureDoesNotPreventOtherClientsFromUpdating()
    {
        using var service = new ClockUpdateService(() => InitialTime, enableTimer: false);
        var updates = 0;
        service.Subscribe(new object(), (_, _) => throw new InvalidOperationException("Test failure"), requiresSecondUpdates: true);
        service.Subscribe(new object(), (_, _) => updates++, requiresSecondUpdates: false);

        service.DispatchTick(InitialTime.AddMinutes(1));

        Assert.AreEqual(1, updates);
    }
}
