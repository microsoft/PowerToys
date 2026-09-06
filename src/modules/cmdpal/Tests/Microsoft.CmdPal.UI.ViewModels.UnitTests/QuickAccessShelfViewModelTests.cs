// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class QuickAccessShelfViewModelTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RebuildCompleted_ReportsAppliedItemsEvenWhenTheShelfStaysEmpty(bool hasItems)
    {
        var viewModel = CreateViewModelWithPendingRebuild();
        var completions = 0;
        var hasItemsChanges = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(QuickAccessShelfViewModel.HasItems))
            {
                hasItemsChanges++;
            }
        };
        viewModel.RebuildCompleted += (sender, succeeded) =>
        {
            Assert.AreSame(viewModel, sender);
            Assert.IsTrue(succeeded);
            Assert.AreEqual(hasItems, viewModel.HasItems);
            Assert.AreEqual(hasItems ? 1 : 0, viewModel.VisibleItemCount);
            completions++;
        };

        viewModel.SetItemConfiguration(RecentCommandsPlacement.Hidden, 8, 8);
        viewModel.SetItemConfiguration(RecentCommandsPlacement.AfterPinned, 8, 8, forceRebuild: true);
        Assert.IsFalse(viewModel.HasItems);
        Assert.AreEqual(0, completions);

        QuickAccessShelfItem[] rebuiltItems = hasItems ? [CreateRecentItem()] : [];
        CompleteRebuild(viewModel, Task.FromResult(rebuiltItems));

        Assert.AreEqual(1, completions);
        Assert.AreEqual(hasItems ? 1 : 0, hasItemsChanges);
    }

    [TestMethod]
    public void SetItemConfiguration_CanForceACompletionForUnchangedConfiguration()
    {
        var viewModel = CreateViewModelWithPendingRebuild();
        viewModel.SetItemConfiguration(RecentCommandsPlacement.Hidden, 8, 8);
        var version = GetRebuildVersion(viewModel);

        viewModel.SetItemConfiguration(RecentCommandsPlacement.Hidden, 8, 8);
        Assert.AreEqual(version, GetRebuildVersion(viewModel));

        viewModel.SetItemConfiguration(RecentCommandsPlacement.Hidden, 8, 8, forceRebuild: true);
        Assert.AreEqual(version + 1, GetRebuildVersion(viewModel));

        var completions = 0;
        viewModel.RebuildCompleted += (_, succeeded) =>
        {
            Assert.IsTrue(succeeded);
            completions++;
        };
        CompleteRebuild(viewModel, Task.FromResult(Array.Empty<QuickAccessShelfItem>()));
        Assert.AreEqual(1, completions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RebuildCompleted_ReportsFailureWithoutReplacingItems(bool canceled)
    {
        var viewModel = CreateViewModelWithPendingRebuild();
        var previousItem = CreateRecentItem();
        viewModel.Items.Add(previousItem);
        bool? succeeded = null;
        viewModel.RebuildCompleted += (_, result) => succeeded = result;

        var rebuild = canceled
            ? Task.FromCanceled<QuickAccessShelfItem[]>(new CancellationToken(canceled: true))
            : Task.FromException<QuickAccessShelfItem[]>(new InvalidOperationException("Rebuild failed"));
        CompleteRebuild(viewModel, rebuild);

        Assert.IsFalse(succeeded ?? true);
        Assert.AreSame(previousItem, viewModel.Items[0]);
    }

    [TestMethod]
    public void RebuildCompleted_DoesNotNotifyAfterDisposal()
    {
        var viewModel = CreateViewModelWithPendingRebuild();
        var completions = 0;
        viewModel.RebuildCompleted += (_, _) => completions++;
        typeof(QuickAccessShelfViewModel).GetField("_isDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel, true);

        CompleteRebuild(viewModel, Task.FromResult(new[] { CreateRecentItem() }));

        Assert.AreEqual(0, completions);
        Assert.IsFalse(viewModel.HasItems);
    }

    private static QuickAccessShelfViewModel CreateViewModelWithPendingRebuild()
    {
        // Isolate completion from the constructor's global provider and All Apps subscriptions.
        var viewModel = (QuickAccessShelfViewModel)RuntimeHelpers.GetUninitializedObject(typeof(QuickAccessShelfViewModel));
        foreach (var property in new[]
        {
            nameof(QuickAccessShelfViewModel.Items),
            nameof(QuickAccessShelfViewModel.VisibleItems),
            nameof(QuickAccessShelfViewModel.OverflowItems),
        })
        {
            typeof(QuickAccessShelfViewModel).GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(viewModel, new ObservableCollection<QuickAccessShelfItem>());
        }

        // Keep the existing worker pending while the test requests the next configuration.
        typeof(QuickAccessShelfViewModel).GetField("_rebuildRunning", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel, 1);
        typeof(QuickAccessShelfViewModel).GetField("_visibleCapacity", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel, int.MaxValue);
        return viewModel;
    }

    private static int GetRebuildVersion(QuickAccessShelfViewModel viewModel) =>
        (int)typeof(QuickAccessShelfViewModel).GetField("_rebuildVersion", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewModel)!;

    private static void CompleteRebuild(QuickAccessShelfViewModel viewModel, Task<QuickAccessShelfItem[]> task)
    {
        var completion = typeof(QuickAccessShelfViewModel).GetMethod("CompleteRebuild", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<int, Task<QuickAccessShelfItem[]>>>(viewModel);
        completion(GetRebuildVersion(viewModel), task);
    }

    private static QuickAccessShelfItem CreateRecentItem() =>
        QuickAccessShelfItem.CreateOrReuse([], new ListItem { Title = "Recent" }, shortcutIndex: 0, startsNewSection: false);
}
