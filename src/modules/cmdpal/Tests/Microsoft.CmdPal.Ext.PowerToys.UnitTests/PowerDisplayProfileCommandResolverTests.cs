// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class PowerDisplayProfileCommandResolverTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task Restore_ReturnsImmediatelyAndSharesOneQueryForAllItems()
    {
        var pending = new TaskCompletionSource<PowerDisplayCliResult<CliProfileListResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerDisplayCliService { GetProfilesHandler = _ => pending.Task };
        var resolver = new PowerDisplayProfileCommandResolver(service);
        Assert.AreEqual(0, service.GetProfilesCallCount);

        var first = resolver.GetCommandItem(3);
        var second = resolver.GetCommandItem(8);
        var firstUpdated = WaitForPropertyAsync(first, nameof(first.Subtitle), () => first.Subtitle.StartsWith("2 monitors", StringComparison.Ordinal));
        var secondUpdated = WaitForPropertyAsync(second, nameof(second.Subtitle), () => second.Subtitle.StartsWith("1 monitor", StringComparison.Ordinal));

        Assert.AreEqual("Power Display profile (#3)", first.Title);
        await service.GetProfilesObserved.Task.WaitAsync(TestTimeout);
        Assert.AreEqual(1, service.GetProfilesCallCount);

        pending.SetResult(PowerDisplayCliResult<CliProfileListResult>.Success(new CliProfileListResult
        {
            Profiles =
            [
                new() { Id = 3, Name = "Cinema", MonitorCount = 2 },
                new() { Id = 8, Name = "Work", MonitorCount = 1 },
            ],
        }));
        await Task.WhenAll(firstUpdated, secondUpdated);

        Assert.AreEqual("Cinema (#3)", first.Title);
        Assert.AreEqual("Work (#8)", second.Title);
        Assert.AreEqual("com.microsoft.powertoys.powerDisplay.applyProfile.3", first.Command!.Id);
        Assert.IsInstanceOfType<ApplyPowerDisplayProfileCommand>(first.Command);
        Assert.AreEqual("Cinema (#3)", resolver.GetCommandItem(3).Title);
        Assert.AreEqual(1, service.GetProfilesCallCount);
    }

    [TestMethod]
    public async Task NewRestorationBatch_RereadsNamesAndRecognizesDeletedProfiles()
    {
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = _ => Task.FromResult(ProfileResult("Cinema")),
        };
        var firstBatch = new PowerDisplayProfileCommandResolver(service);
        var original = firstBatch.GetCommandItem(3);
        await WaitForPropertyAsync(original, nameof(original.Title), () => original.Title == "Cinema (#3)");

        service.GetProfilesHandler = _ => Task.FromResult(ProfileResult("Renamed"));
        var nextBatch = new PowerDisplayProfileCommandResolver(service);
        var renamed = nextBatch.GetCommandItem(3);
        var removed = nextBatch.GetCommandItem(8);
        await Task.WhenAll(
            WaitForPropertyAsync(renamed, nameof(renamed.Title), () => renamed.Title == "Renamed (#3)"),
            WaitForPropertyAsync(removed, nameof(removed.Subtitle), () => removed.Subtitle.Contains("no longer available", StringComparison.Ordinal)));

        Assert.AreEqual(2, service.GetProfilesCallCount);
        Assert.AreEqual("com.microsoft.powertoys.powerDisplay.applyProfile.8", removed.Command!.Id);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedQuery_PreservesInvokableIdsWithoutRetryingPerItem(bool throws)
    {
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = _ => throws
                ? Task.FromException<PowerDisplayCliResult<CliProfileListResult>>(new InvalidOperationException("test failure"))
                : Task.FromResult(PowerDisplayCliResult<CliProfileListResult>.Failure(PowerDisplayCliFailureKind.ProviderUnavailable)),
        };
        var resolver = new PowerDisplayProfileCommandResolver(service);
        var first = resolver.GetCommandItem(3);
        await WaitForPropertyAsync(first, nameof(first.Subtitle), () => first.Subtitle.Contains("couldn't be loaded", StringComparison.Ordinal));
        var second = resolver.GetCommandItem(8);
        await WaitForPropertyAsync(second, nameof(second.Subtitle), () => second.Subtitle.Contains("couldn't be loaded", StringComparison.Ordinal));

        Assert.AreEqual(1, service.GetProfilesCallCount);
        Assert.AreEqual("com.microsoft.powertoys.powerDisplay.applyProfile.3", first.Command!.Id);
        Assert.IsInstanceOfType<ApplyPowerDisplayProfileCommand>(first.Command);
        Assert.AreEqual("Power Display profile (#8)", second.Title);
    }

    [TestMethod]
    public async Task OverlappingBatches_StillResolvePreviouslyReturnedItems()
    {
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstPending = new TaskCompletionSource<PowerDisplayCliResult<CliProfileListResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPending = new TaskCompletionSource<PowerDisplayCliResult<CliProfileListResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryCount = 0;
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = _ =>
            {
                if (Interlocked.Increment(ref queryCount) == 1)
                {
                    firstStarted.TrySetResult(true);
                    return firstPending.Task;
                }

                secondStarted.TrySetResult(true);
                return secondPending.Task;
            },
        };
        using var lifetime = new CancellationTokenSource();
        var firstBatch = new PowerDisplayProfileCommandResolver(service, lifetime.Token);
        var firstItem = firstBatch.GetCommandItem(3);
        await firstStarted.Task.WaitAsync(TestTimeout);

        var secondBatch = new PowerDisplayProfileCommandResolver(service, lifetime.Token);
        var secondItem = secondBatch.GetCommandItem(3);
        await secondStarted.Task.WaitAsync(TestTimeout);

        firstPending.SetResult(ProfileResult("First"));
        await WaitForPropertyAsync(firstItem, nameof(firstItem.Title), () => firstItem.Title == "First (#3)");
        Assert.AreEqual("Power Display profile (#3)", secondItem.Title);
        secondPending.SetResult(ProfileResult("Second"));
        await WaitForPropertyAsync(secondItem, nameof(secondItem.Title), () => secondItem.Title == "Second (#3)");
        Assert.AreEqual(2, service.GetProfilesCallCount);
    }

    [TestMethod]
    public async Task ProviderShutdown_CancelsPendingCliQuery()
    {
        var requestReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<PowerDisplayCliResult<CliProfileListResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = token =>
            {
                token.Register(() =>
                {
                    cancellationObserved.TrySetResult(true);
                    pending.TrySetCanceled(token);
                });
                requestReady.TrySetResult(true);
                return pending.Task;
            },
        };
        using var cancellation = new CancellationTokenSource();
        var resolver = new PowerDisplayProfileCommandResolver(service, cancellation.Token);
        var item = resolver.GetCommandItem(3);
        await requestReady.Task.WaitAsync(TestTimeout);

        cancellation.Cancel();
        await cancellationObserved.Task.WaitAsync(TestTimeout);

        Assert.AreEqual("Power Display profile (#3)", item.Title);
        Assert.AreEqual(1, service.GetProfilesCallCount);
    }

    private static PowerDisplayCliResult<CliProfileListResult> ProfileResult(string name)
        => PowerDisplayCliResult<CliProfileListResult>.Success(new CliProfileListResult
        {
            Profiles = [new() { Id = 3, Name = name, MonitorCount = 1 }],
        });

    private static async Task WaitForPropertyAsync(ListItem item, string propertyName, Func<bool> predicate)
    {
        var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TypedEventHandler<object, IPropChangedEventArgs> handler = (_, args) =>
        {
            if (args.PropertyName == propertyName && predicate())
            {
                changed.TrySetResult(true);
            }
        };
        item.PropChanged += handler;
        try
        {
            if (!predicate())
            {
                await changed.Task.WaitAsync(TestTimeout);
            }
        }
        finally
        {
            item.PropChanged -= handler;
        }
    }
}
