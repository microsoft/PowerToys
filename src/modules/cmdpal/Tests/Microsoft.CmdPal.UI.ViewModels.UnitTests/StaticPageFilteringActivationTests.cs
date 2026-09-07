// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public sealed partial class StaticPageFilteringTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Activation_WaitsForPublishedQueryAndItsSelection(bool secondary)
    {
        var alpha = Item("Alpha");
        var beta = Item("Beta");
        var secondaryCommand = new NoOpCommand { Name = "Beta secondary" };
        beta.MoreCommands = [new CommandContextItem(secondaryCommand)];
        using var fixture = new Fixture(alpha, beta);
        using var commands = new CommandObserver();
        var alphaVm = fixture.ViewModel.FilteredItems[0];
        var betaVm = fixture.ViewModel.FilteredItems[1];
        Assert.IsTrue(betaVm.SafeSlowInit());
        fixture.CompleteSelection(alphaVm);
        var oldVersion = fixture.ViewModel.PublishedFilterVersion;

        fixture.ViewModel.SearchTextBox = "Beta";
        Activate(fixture, alphaVm, secondary);
        Assert.HasCount(0, commands.Messages);
        fixture.Worker.ExecuteAll();
        Assert.HasCount(0, commands.Messages);
        fixture.Ui.ExecuteAll();
        Assert.HasCount(0, commands.Messages);
        fixture.ViewModel.CompleteStaticFilterSelection(oldVersion, alphaVm);
        Assert.IsFalse(fixture.ViewModel.CompleteStaticFilterSelection(fixture.ViewModel.PublishedFilterVersion, alphaVm));
        Assert.HasCount(0, commands.Messages);

        fixture.CompleteSelection(betaVm);

        Assert.HasCount(1, commands.Messages);
        Assert.AreSame(secondary ? secondaryCommand : beta.Command, commands.Messages[0].Command.Unsafe);
        Assert.AreSame(beta, commands.Messages[0].Context);
        fixture.ViewModel.CompleteStaticFilterSelection(fixture.ViewModel.PublishedFilterVersion, betaVm);
        Assert.HasCount(1, commands.Messages);
    }

    [TestMethod]
    public void Activation_WithNoPreviousSelection_WaitsForMatchingItem()
    {
        var beta = Item("Beta");
        using var fixture = new Fixture(beta);
        using var commands = new CommandObserver();

        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.ViewModel.InvokeItemCommand.Execute(null);
        fixture.Drain();
        Assert.HasCount(0, commands.Messages);
        Assert.IsFalse(fixture.ViewModel.CompleteStaticFilterSelection(fixture.ViewModel.PublishedFilterVersion, null));
        Assert.HasCount(0, commands.Messages);

        fixture.CompleteSelection(fixture.ViewModel.FilteredItems.Single());

        Assert.HasCount(1, commands.Messages);
        Assert.AreSame(beta, commands.Messages[0].Context);
    }

    [TestMethod]
    public void Activation_WithNoMatches_DoesNotRunPreviousSelection()
    {
        using var fixture = new Fixture(Item("Alpha"));
        using var commands = new CommandObserver();
        var previous = fixture.ViewModel.FilteredItems.Single();
        fixture.CompleteSelection(previous);

        fixture.ViewModel.SearchTextBox = "no matching item";
        fixture.ViewModel.InvokeItemCommand.Execute(previous);
        fixture.Drain();
        fixture.CompleteSelection(null);

        Assert.HasCount(0, fixture.ViewModel.FilteredItems);
        Assert.HasCount(0, commands.Messages);
        fixture.ViewModel.SearchTextBox = string.Empty;
        fixture.Drain();
        fixture.CompleteSelection(fixture.ViewModel.FilteredItems.Single());
        Assert.HasCount(0, commands.Messages);
    }

    [TestMethod]
    public void NewQuery_CancelsDeferredActivation()
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"), Item("Gamma"));
        using var commands = new CommandObserver();
        var previous = fixture.ViewModel.FilteredItems[0];
        fixture.CompleteSelection(previous);

        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.ViewModel.InvokeItemCommand.Execute(previous);
        fixture.Worker.ExecuteAll();
        fixture.ViewModel.SearchTextBox = "Gamma";
        fixture.Drain();
        fixture.CompleteSelection(fixture.ViewModel.FilteredItems.Single());

        CollectionAssert.AreEqual(GammaTitles, fixture.Titles);
        Assert.HasCount(0, commands.Messages);
    }

    [TestMethod]
    public void ItemRefresh_ResolvesDeferredActivationAgainstNewGeneration()
    {
        var replacement = Item("Beta replacement");
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"));
        using var commands = new CommandObserver();
        var previous = fixture.ViewModel.FilteredItems[0];
        fixture.CompleteSelection(previous);

        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.ViewModel.InvokeItemCommand.Execute(previous);
        fixture.Worker.ExecuteAll();
        fixture.Page.SetItems(replacement);
        fixture.Page.Refresh();
        fixture.Drain();
        Assert.HasCount(0, commands.Messages);

        fixture.CompleteSelection(fixture.ViewModel.FilteredItems.Single());

        Assert.HasCount(1, commands.Messages);
        Assert.AreSame(replacement, commands.Messages[0].Context);
    }

    [TestMethod]
    public void ReentrantActivation_WaitsUntilPublicationCompletes()
    {
        var beta = Item("Beta");
        using var fixture = new Fixture(Item("Alpha"), beta);
        using var commands = new CommandObserver();
        var previous = fixture.ViewModel.FilteredItems[0];
        fixture.CompleteSelection(previous);
        NotifyCollectionChangedEventHandler onCollectionChanged = (_, _) =>
        {
            fixture.ViewModel.InvokeItemCommand.Execute(previous);
            Assert.HasCount(0, commands.Messages);
        };
        fixture.ViewModel.FilteredItems.CollectionChanged += onCollectionChanged;
        fixture.ViewModel.ItemsUpdated += (_, _) =>
        {
            fixture.CompleteSelection(fixture.ViewModel.FilteredItems.Single());
            Assert.HasCount(0, commands.Messages);
        };

        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.Drain();
        fixture.ViewModel.FilteredItems.CollectionChanged -= onCollectionChanged;

        Assert.HasCount(1, commands.Messages);
        Assert.AreSame(beta, commands.Messages[0].Context);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RendererDetachOrDisposal_CancelsDeferredActivation(bool dispose)
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"));
        using var commands = new CommandObserver();
        var previous = fixture.ViewModel.FilteredItems[0];
        fixture.CompleteSelection(previous);
        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.ViewModel.InvokeItemCommand.Execute(previous);
        fixture.Worker.ExecuteAll();

        if (dispose)
        {
            fixture.ViewModel.Dispose();
        }
        else
        {
            fixture.ViewModel.CancelPendingActivation();
        }

        fixture.Ui.ExecuteAll();
        fixture.CompleteSelection(fixture.ViewModel.FilteredItems[0]);

        Assert.HasCount(0, commands.Messages);
    }

    [TestMethod]
    public void CurrentSelection_ActivatesImmediately()
    {
        var alpha = Item("Alpha");
        using var fixture = new Fixture(alpha);
        using var commands = new CommandObserver();
        var selected = fixture.ViewModel.FilteredItems.Single();
        fixture.CompleteSelection(selected);

        fixture.ViewModel.InvokeItemCommand.Execute(selected);

        Assert.HasCount(1, commands.Messages);
        Assert.AreSame(alpha, commands.Messages[0].Context);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DeferredSecondaryActivation_WaitsForHydrationUnlessSelectionChanges(bool changeSelection)
    {
        using var release = new ManualResetEventSlim();
        var secondary = new NoOpCommand { Name = "Secondary" };
        var beta = new DeferredCommandsItem(release, secondary) { Title = "Beta" };
        using var fixture = new Fixture(Item("Alpha"), beta, Item("Beta other"));
        using var commands = new CommandObserver();
        var previous = fixture.ViewModel.FilteredItems[0];
        fixture.CompleteSelection(previous);
        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.ViewModel.InvokeSecondaryCommandCommand.Execute(previous);
        fixture.Drain();
        var selected = fixture.ViewModel.FilteredItems.First(item => ReferenceEquals(item.Model.Unsafe, beta));
        var hydrated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        selected.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ListItemViewModel.TextToSuggest))
            {
                hydrated.TrySetResult();
            }
        };

        fixture.CompleteSelection(selected);
        try
        {
            await beta.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Ui.ExecuteAll();
            Assert.HasCount(0, commands.Messages);

            if (changeSelection)
            {
                fixture.CompleteSelection(fixture.ViewModel.FilteredItems.First(item => !ReferenceEquals(item, selected)));
            }

            release.Set();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!hydrated.Task.IsCompleted || (!changeSelection && commands.Messages.Count == 0))
            {
                await fixture.Ui.WhenQueued.WaitAsync(timeout.Token);
                fixture.Ui.ExecuteAll();
            }

            Assert.HasCount(changeSelection ? 0 : 1, commands.Messages);
            if (!changeSelection)
            {
                Assert.AreSame(secondary, commands.Messages[0].Command.Unsafe);
                Assert.AreSame(beta, commands.Messages[0].Context);
            }
        }
        finally
        {
            release.Set();
        }
    }

    private static void Activate(Fixture fixture, ListItemViewModel? item, bool secondary)
    {
        if (secondary)
        {
            fixture.ViewModel.InvokeSecondaryCommandCommand.Execute(item);
        }
        else
        {
            fixture.ViewModel.InvokeItemCommand.Execute(item);
        }
    }

    private sealed class CommandObserver : IDisposable
    {
        internal List<PerformCommandMessage> Messages { get; } = [];

        internal CommandObserver()
        {
            WeakReferenceMessenger.Default.Register<CommandObserver, PerformCommandMessage>(
                this,
                static (observer, message) => observer.Messages.Add(message));
        }

        public void Dispose() => WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private sealed partial class DeferredCommandsItem(ManualResetEventSlim release, ICommand command) : ListItem(new NoOpCommand())
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override IContextItem[] MoreCommands
        {
            get
            {
                Entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Command hydration was not released.");
                }

                return [new CommandContextItem(command)];
            }

            set => throw new NotSupportedException();
        }
    }
}
