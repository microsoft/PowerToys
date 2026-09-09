// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public sealed partial class StaticPageFilteringTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedRefresh_ResumesPendingAndFutureQueries(bool canceledByProvider)
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"));
        var updatesDuringFetch = -1;
        fixture.Page.BeforeGetItems = () =>
        {
            fixture.ViewModel.SearchTextBox = "Beta";
            fixture.Drain();
            updatesDuringFetch = fixture.Updates.Count;
            if (canceledByProvider)
            {
                throw new OperationCanceledException();
            }

            throw new InvalidOperationException("Provider refresh failed.");
        };

        fixture.Page.Refresh();
        fixture.Drain();
        Assert.AreEqual(0, updatesDuringFetch);
        CollectionAssert.AreEqual(BetaTitles, fixture.Titles);
        Assert.HasCount(1, fixture.Updates);
        Assert.IsTrue(fixture.Updates[0].ForceFirstItem);
        if (!canceledByProvider)
        {
            StringAssert.Contains(fixture.ViewModel.ErrorMessage, "Provider refresh failed.");
        }

        fixture.ViewModel.SearchTextBox = "Alpha";
        fixture.Drain();
        CollectionAssert.AreEqual(AlphaTitles, fixture.Titles);

        fixture.Page.BeforeGetItems = null;
        fixture.Page.SetItems(Item("Gamma"));
        fixture.Page.Refresh();
        fixture.ViewModel.SearchTextBox = "Gamma";
        fixture.Drain();
        CollectionAssert.AreEqual(GammaTitles, fixture.Titles);
    }

    [TestMethod]
    public void FailedRefresh_ReplaysActivationOnlyAfterRecoveredSelection()
    {
        var beta = Item("Beta");
        using var fixture = new Fixture(Item("Alpha"), beta);
        using var commands = new CommandObserver();
        var previous = fixture.ViewModel.FilteredItems[0];
        fixture.CompleteSelection(previous);
        fixture.Page.BeforeGetItems = () =>
        {
            fixture.ViewModel.SearchTextBox = "Beta";
            fixture.ViewModel.InvokeItemCommand.Execute(previous);
            throw new InvalidOperationException("Provider refresh failed.");
        };

        fixture.Page.Refresh();
        fixture.Drain();
        StringAssert.Contains(fixture.ViewModel.ErrorMessage, "Provider refresh failed.");
        Assert.HasCount(0, commands.Messages);
        fixture.CompleteSelection(fixture.ViewModel.FilteredItems.Single());

        Assert.HasCount(1, commands.Messages);
        Assert.AreSame(beta, commands.Messages[0].Context);
    }

    [TestMethod]
    public async Task OlderFailedRefresh_DoesNotReleaseQueriesDuringNewerFetch()
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"));
        using var releaseOld = new ManualResetEventSlim();
        using var releaseNew = new ManualResetEventSlim();
        var oldEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        fixture.Page.BeforeGetItems = () =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                oldEntered.SetResult();
                Assert.IsTrue(releaseOld.Wait(TimeSpan.FromSeconds(5)));
                throw new InvalidOperationException("Old refresh failed.");
            }

            newEntered.SetResult();
            Assert.IsTrue(releaseNew.Wait(TimeSpan.FromSeconds(5)));
        };

        var oldFetch = Task.Run(() => fixture.Page.Refresh());
        Task? newFetch = null;
        try
        {
            await oldEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            newFetch = Task.Run(() => fixture.Page.Refresh());
            await newEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.ViewModel.SearchTextBox = "Beta";
            releaseOld.Set();
            await oldFetch.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Drain();

            StringAssert.Contains(fixture.ViewModel.ErrorMessage, "Old refresh failed.");
            Assert.HasCount(0, fixture.Updates);
            CollectionAssert.AreEqual(InitialTitles, fixture.Titles);

            releaseNew.Set();
            await newFetch.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Drain();

            CollectionAssert.AreEqual(BetaTitles, fixture.Titles);
            Assert.HasCount(1, fixture.Updates);
        }
        finally
        {
            releaseOld.Set();
            releaseNew.Set();
            await oldFetch.WaitAsync(TimeSpan.FromSeconds(5));
            if (newFetch is not null)
            {
                await newFetch.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    [TestMethod]
    public void FailedRefresh_AfterDisposal_DoesNotRestartFiltering()
    {
        using var fixture = new Fixture(Item("Alpha"));
        fixture.Page.BeforeGetItems = () =>
        {
            fixture.ViewModel.SearchTextBox = "Beta";
            fixture.ViewModel.Dispose();
            throw new InvalidOperationException("Provider refresh failed.");
        };

        fixture.Page.Refresh();
        fixture.Drain();

        StringAssert.Contains(fixture.ViewModel.ErrorMessage, "Provider refresh failed.");
        Assert.AreEqual(0, fixture.Worker.Count);
        Assert.HasCount(0, fixture.Updates);
        CollectionAssert.AreEqual(AlphaTitles, fixture.Titles);
    }
}
