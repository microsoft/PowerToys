// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class ProfileOperationCoordinatorTests
{
    private static readonly bool[] ExpectedRunningStates = { true, false };

    [TestMethod]
    public async Task RunAsync_ReportsLoadingUntilOperationCompletes()
    {
        using var coordinator = new ProfileOperationCoordinator();
        var states = new List<bool>();
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.IsRunningChanged += (_, _) => states.Add(coordinator.IsRunning);

        var operation = coordinator.RunAsync(_ => completion.Task);

        Assert.IsTrue(coordinator.IsRunning);
        completion.SetResult(42);
        Assert.AreEqual(42, await operation);
        Assert.IsFalse(coordinator.IsRunning);
        CollectionAssert.AreEqual(ExpectedRunningStates, states);
    }

    [TestMethod]
    public async Task RunAsync_SerializesOverlappingOperations()
    {
        using var coordinator = new ProfileOperationCoordinator();
        var firstCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;

        var first = coordinator.RunAsync(_ => firstCompletion.Task);
        var second = coordinator.RunAsync(_ =>
        {
            secondStarted = true;
            return Task.FromResult(2);
        });

        Assert.IsFalse(secondStarted);
        firstCompletion.SetResult(1);
        await Task.WhenAll(first, second);
        Assert.IsTrue(secondStarted);
    }

    [TestMethod]
    public async Task RunAsync_OperationFailure_ResetsLoadingState()
    {
        using var coordinator = new ProfileOperationCoordinator();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => coordinator.RunAsync<int>(
                _ => Task.FromException<int>(new InvalidOperationException("failure"))));

        Assert.IsFalse(coordinator.IsRunning);
    }

    [TestMethod]
    public async Task RunAsync_IsRunningChangedThrowingOnStart_StillReleasesGate()
    {
        using var coordinator = new ProfileOperationCoordinator();
        var thrownOnce = false;

        coordinator.IsRunningChanged += (_, _) =>
        {
            if (coordinator.IsRunning && !thrownOnce)
            {
                thrownOnce = true;
                throw new InvalidOperationException("boom");
            }
        };

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => coordinator.RunAsync(_ => Task.CompletedTask));

        Assert.IsFalse(coordinator.IsRunning);

        await coordinator.RunAsync(_ => Task.CompletedTask);
        Assert.IsFalse(coordinator.IsRunning);
        Assert.IsTrue(thrownOnce);
    }
}
