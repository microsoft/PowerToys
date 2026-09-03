// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class GridItemsViewModelTests
{
    // Serialize presentation work and the VM's asynchronous property notifications,
    // just as the real view's UI scheduler does.
    private static readonly ConcurrentExclusiveSchedulerPair _schedulers = new();

    [TestMethod]
    public Task Groups_PreserveContiguousRunsAndStructuralRows() => OnPresentationThread(() =>
    {
        var first = Tile();
        var second = Tile();
        var third = Tile();
        var header = Header("Same title");
        var repeatedTitle = Header("Same title");
        var separator = Header();
        ObservableCollection<ListItemViewModel> source = [first, header, second, separator, repeatedTitle, third, Header("Trailing")];
        using var grid = CreateGrid(source);

        Assert.AreEqual(5, grid.Groups.Count);
        Assert.AreEqual(3, grid.ItemCount);
        Assert.IsNull(grid.Groups[0].Header);
        Assert.AreSame(first, grid.Groups[0].Items[0]);
        Assert.AreSame(header, grid.Groups[1].Header);
        Assert.AreSame(second, grid.Groups[1].Items[0]);
        Assert.IsTrue(grid.Groups[2].IsSeparator);
        Assert.AreEqual(0, grid.Groups[2].Items.Count);
        Assert.AreSame(repeatedTitle, grid.Groups[3].Header);
        Assert.AreSame(third, grid.Groups[3].Items[0]);
        Assert.AreEqual(0, grid.Groups[4].Items.Count);
        Assert.AreSame(grid.Groups[3], grid.GroupFromItemIndex(2));
        Assert.IsNull(grid.GroupFromItemIndex(3));
    });

    [TestMethod]
    public Task Updates_ReuseGroupsAndTilesWithoutResetOrHeaderNotifications() => OnPresentationThread(() =>
    {
        var header = Header("Group");
        var first = Tile();
        var second = Tile();
        var third = Tile();
        ObservableCollection<ListItemViewModel> source = [header, first, second];
        using var grid = CreateGrid(source);
        var group = grid.Groups[0];
        var resetCount = 0;
        var headerNotificationCount = 0;
        void Changed(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                resetCount++;
            }
        }

        grid.Groups.CollectionChanged += Changed;
        group.Items.CollectionChanged += Changed;
        group.PropertyChanged += (_, _) => headerNotificationCount++;

        source.Add(third);
        grid.Synchronize();
        Assert.AreSame(group, grid.Groups[0]);
        Assert.AreSame(first, group.Items[0]);
        Assert.AreSame(third, group.Items[2]);

        source.Move(3, 1);
        grid.Synchronize();
        Assert.AreSame(group, grid.Groups[0]);
        CollectionAssert.AreEqual(new[] { third, first, second }, group.Items);
        Assert.AreEqual(0, grid.IndexOf(third));
        Assert.AreEqual(2, grid.IndexOf(second));
        Assert.AreEqual(0, resetCount);
        Assert.AreEqual(0, headerNotificationCount);
    });

    [TestMethod]
    public Task DuplicateReferences_PreserveEachHeaderOccurrence() => OnPresentationThread(() =>
    {
        var header = Header("Repeated");
        var item = Tile();
        ObservableCollection<ListItemViewModel> source = [header, item, header, item];
        using var grid = CreateGrid(source);
        var firstGroup = grid.Groups[0];
        var secondGroup = grid.Groups[1];

        Assert.AreNotSame(firstGroup, secondGroup);
        Assert.AreEqual(2, grid.ItemCount);
        Assert.AreEqual(0, grid.IndexOf(item));
        Assert.AreSame(secondGroup, grid.GroupFromItemIndex(1));

        source.Add(Tile());
        grid.Synchronize();
        Assert.AreSame(firstGroup, grid.Groups[0]);
        Assert.AreSame(secondGroup, grid.Groups[1]);
    });

    [TestMethod]
    public Task EqualModels_PreserveTheSourcesWrapperIdentities() => OnPresentationThread(() =>
    {
        var headerModel = new Separator("Repeated model");
        var firstHeader = new TestItem(headerModel);
        var secondHeader = new TestItem(headerModel);
        var tileModel = new ListItem(new NoOpCommand());
        var first = new TestItem(tileModel);
        var replacement = new TestItem(tileModel);
        ObservableCollection<ListItemViewModel> source = [firstHeader, first, secondHeader, Tile()];
        using var grid = CreateGrid(source);
        var firstGroup = grid.Groups[0];
        var secondGroup = grid.Groups[1];

        source[1] = replacement;
        grid.Synchronize();
        Assert.AreSame(firstGroup, grid.Groups[0]);
        Assert.AreSame(secondGroup, grid.Groups[1]);
        Assert.AreSame(firstHeader, firstGroup.Header);
        Assert.AreSame(secondHeader, secondGroup.Header);
        Assert.AreSame(replacement, firstGroup.Items[0]);
        Assert.AreEqual(-1, grid.IndexOf(first));
        Assert.AreEqual(0, grid.IndexOf(replacement));
    });

    [TestMethod]
    public Task LateHeaderMetadata_RegroupsAndUpdatesHeaderPresentation() => OnPresentationThread(() =>
    {
        var model = new Separator("Late section");
        var lateHeader = new TestItem(model, initializeMetadata: false);
        var item = Tile();
        ObservableCollection<ListItemViewModel> source = [lateHeader, item];
        using var grid = CreateGrid(source);
        Assert.AreEqual(2, grid.ItemCount);

        lateHeader.RefreshMetadata();
        Assert.IsTrue(grid.HasPendingChanges);
        grid.Synchronize();
        Assert.AreEqual(1, grid.ItemCount);
        Assert.AreSame(lateHeader, grid.Groups[0].Header);
        Assert.AreEqual("Late section", grid.Groups[0].Title);

        var group = grid.Groups[0];
        model.Title = string.Empty;
        lateHeader.RefreshMetadata();
        grid.Synchronize();
        Assert.AreSame(group, grid.Groups[0]);
        Assert.IsTrue(group.IsSeparator);
        Assert.IsFalse(group.IsSectionHeader);
        Assert.AreEqual(string.Empty, group.ToString());

        model.Title = "Renamed";
        lateHeader.RefreshMetadata();
        grid.Synchronize();
        Assert.AreSame(group, grid.Groups[0]);
        Assert.AreEqual("Renamed", group.Title);
        Assert.IsTrue(group.IsSectionHeader);
        Assert.AreEqual("Renamed", group.ToString());
    });

    [TestMethod]
    public Task RemovingBoundary_MergesTilesWithoutChangingTheirOrder() => OnPresentationThread(() =>
    {
        var first = Tile();
        var second = Tile();
        var header = Header("Boundary");
        ObservableCollection<ListItemViewModel> source = [first, header, second];
        using var grid = CreateGrid(source);
        var prefix = grid.Groups[0];

        source.Remove(header);
        grid.Synchronize();
        Assert.AreEqual(1, grid.Groups.Count);
        Assert.AreSame(prefix, grid.Groups[0]);
        CollectionAssert.AreEqual(new[] { first, second }, prefix.Items);
    });

    [TestMethod]
    public Task ReentrantSourceChange_RemainsPendingUntilNextSynchronization() => OnPresentationThread(() =>
    {
        var first = Tile();
        var second = Tile();
        var third = Tile();
        ObservableCollection<ListItemViewModel> source = [first];
        using var grid = CreateGrid(source);
        var changed = false;
        grid.Groups[0].Items.CollectionChanged += (_, _) =>
        {
            if (!changed)
            {
                changed = true;
                source.Add(third);
                Assert.IsFalse(grid.Synchronize());
            }
        };

        source.Add(second);
        grid.Synchronize();
        Assert.IsTrue(grid.HasPendingChanges);
        grid.Synchronize();
        Assert.IsFalse(grid.HasPendingChanges);
        CollectionAssert.AreEqual(new[] { first, second, third }, grid.Groups[0].Items);
    });

    [TestMethod]
    public Task DetachingSource_UnsubscribesAndReattachingReusesProjection() => OnPresentationThread(() =>
    {
        var header = Header("Group");
        var item = Tile();
        ObservableCollection<ListItemViewModel> source = [header, item];
        using var grid = CreateGrid(source);
        var group = grid.Groups[0];
        grid.SetSource(null);
        Assert.AreSame(group, grid.Groups[0]);

        source.Add(Tile());
        grid.SetSource(source);
        grid.Synchronize();
        Assert.AreSame(group, grid.Groups[0]);
        Assert.AreEqual(2, group.Items.Count);

        grid.SetSource(null);
        grid.Synchronize();
        source.Add(Tile());
        header.RefreshMetadata();
        Assert.IsFalse(grid.HasPendingChanges);
    });

    [TestMethod]
    public Task NonStructuralNotifications_DoNotRebuildGroups() => OnPresentationThread(() =>
    {
        var item = Tile();
        using var grid = CreateGrid([item]);
        var version = grid.Version;
        item.NotifyTitleChanged();
        item.RefreshMetadata();
        Assert.IsFalse(grid.Synchronize());
        Assert.AreEqual(version, grid.Version);
    });

    [TestMethod]
    public async Task GalleryLayoutFlags_NotifyOnThePresentationScheduler()
    {
        TestItem item = null!;
        var titleChanged = new TaskCompletionSource<TaskScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subtitleChanged = new TaskCompletionSource<TaskScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        await OnPresentationThread(() =>
        {
            item = Tile();
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ListItemViewModel.LayoutShowsTitle))
                {
                    titleChanged.TrySetResult(TaskScheduler.Current);
                }
                else if (e.PropertyName == nameof(ListItemViewModel.LayoutShowsSubtitle))
                {
                    subtitleChanged.TrySetResult(TaskScheduler.Current);
                }
            };
        });

        await Task.Run(() =>
        {
            item.LayoutShowsTitle = true;
            item.LayoutShowsSubtitle = true;
            item.ApplyPendingUpdates();
        });

        Assert.AreSame(_schedulers.ExclusiveScheduler, await titleChanged.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreSame(_schedulers.ExclusiveScheduler, await subtitleChanged.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public Task Navigation_PreservesColumnAcrossPartialRowsAndWrapsBoundaries() => OnPresentationThread(() =>
    {
        var groups = new[] { Group(7), Group(0, header: true), Group(10, header: true) };
        var layout = new GridNavigationLayout(groups, columns: 3, itemHeight: 100, headerHeight: 36);

        Assert.AreEqual(6, layout.MoveVertical(5, down: true, column: 2, wrap: true));
        Assert.AreEqual(9, layout.MoveVertical(6, down: true, column: 2, wrap: true));
        Assert.AreEqual(6, layout.MoveVertical(9, down: false, column: 2, wrap: true));
        Assert.AreEqual(2, layout.GetColumn(9));
        Assert.AreEqual(16, layout.MoveVertical(0, down: false, column: 0, wrap: true));
        Assert.AreEqual(0, layout.MoveVertical(16, down: true, column: 0, wrap: true));
        Assert.AreEqual(16, layout.MoveVertical(2, down: false, column: 2, wrap: true));
        Assert.AreEqual(2, layout.MoveVertical(16, down: true, column: 2, wrap: true));
        Assert.AreEqual(0, layout.MoveVertical(0, down: false, column: 0, wrap: false));
        Assert.AreEqual(16, layout.MoveVertical(16, down: true, column: 0, wrap: false));
    });

    [TestMethod]
    public void HorizontalNavigation_WrapsBothBoundaries()
    {
        Assert.AreEqual(-1, GridNavigationLayout.MoveHorizontal(-1, increaseIndex: true, itemCount: 0));
        Assert.AreEqual(0, GridNavigationLayout.MoveHorizontal(-1, increaseIndex: true, itemCount: 5));
        Assert.AreEqual(1, GridNavigationLayout.MoveHorizontal(0, increaseIndex: true, itemCount: 5));
        Assert.AreEqual(0, GridNavigationLayout.MoveHorizontal(4, increaseIndex: true, itemCount: 5));
        Assert.AreEqual(3, GridNavigationLayout.MoveHorizontal(4, increaseIndex: false, itemCount: 5));
        Assert.AreEqual(4, GridNavigationLayout.MoveHorizontal(0, increaseIndex: false, itemCount: 5));
    }

    [TestMethod]
    public Task PageNavigation_UsesRowsAndHeaderExtents() => OnPresentationThread(() =>
    {
        var groups = new[] { Group(7), Group(0, header: true), Group(10, header: true) };
        var layout = new GridNavigationLayout(groups, columns: 3, itemHeight: 100, headerHeight: 36);

        Assert.AreEqual(6, layout.MovePage(2, down: true, column: 2, viewportHeight: 220));
        Assert.AreEqual(9, layout.MovePage(6, down: true, column: 2, viewportHeight: 150));
        Assert.AreEqual(6, layout.MovePage(9, down: false, column: 2, viewportHeight: 150));
        Assert.AreEqual(16, layout.MovePage(2, down: true, column: 2, viewportHeight: 10000));
        Assert.AreEqual(2, layout.MovePage(16, down: false, column: 2, viewportHeight: 10000));
        Assert.AreEqual(3, layout.MovePage(0, down: true, column: 0, viewportHeight: 20));
        Assert.AreEqual(2, layout.MovePage(2, down: false, column: 2, viewportHeight: 10000));
        Assert.AreEqual(16, layout.MovePage(16, down: true, column: 0, viewportHeight: 10000));
    });

    [TestMethod]
    public Task Navigation_WorksFarOutsideTheViewportAndAfterColumnChanges() => OnPresentationThread(() =>
    {
        var groups = new[] { Group(10000) };
        var layout = new GridNavigationLayout(groups, columns: 6, itemHeight: 100, headerHeight: 36);
        Assert.AreEqual(6005, layout.MoveVertical(5999, down: true, column: 5, wrap: true));
        Assert.AreEqual(5030, layout.MovePage(5000, down: true, column: 2, viewportHeight: 500));

        layout = new GridNavigationLayout(groups, columns: 4, itemHeight: 100, headerHeight: 36);
        Assert.AreEqual(5004, layout.MoveVertical(5000, down: true, column: 0, wrap: true));
        Assert.AreEqual(5020, layout.MovePage(5000, down: true, column: 0, viewportHeight: 500));
    });

    [TestMethod]
    public Task Navigation_HandlesOnlyHeadersAndAnEmptyGrid() => OnPresentationThread(() =>
    {
        var layout = new GridNavigationLayout([Group(0, header: true)], columns: 4, itemHeight: 100, headerHeight: 36);
        Assert.AreEqual(-1, layout.MoveVertical(-1, down: true, column: 0, wrap: true));
        Assert.AreEqual(-1, layout.MovePage(-1, down: true, column: 0, viewportHeight: 500));

        layout = new GridNavigationLayout([], columns: 4, itemHeight: 100, headerHeight: 36);
        Assert.AreEqual(-1, layout.MoveVertical(-1, down: true, column: 0, wrap: true));
    });

    private static Task OnPresentationThread(Action action)
        => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, _schedulers.ExclusiveScheduler);

    private static GridItemsViewModel CreateGrid(ObservableCollection<ListItemViewModel> source)
    {
        var result = new GridItemsViewModel();
        result.SetSource(source);
        result.Synchronize();
        return result;
    }

    private static TestItem Tile() => new(new ListItem(new NoOpCommand()) { Title = "Tile" });

    private static TestItem Header(string title = "") => new(new Separator(title));

    private static GridItemGroupViewModel Group(int count, bool header = false)
    {
        var result = new GridItemGroupViewModel(header ? Header("Group") : null, 0);
        var item = Tile();
        for (var i = 0; i < count; i++)
        {
            result.Items.Add(item);
        }

        return result;
    }

    private sealed partial class TestItem : ListItemViewModel
    {
        private readonly TestPageContext _context;

        public TestItem(IListItem model, bool initializeMetadata = true)
            : this(model, new TestPageContext(), initializeMetadata)
        {
        }

        private TestItem(IListItem model, TestPageContext context, bool initializeMetadata)
            : base(model, new(context), DefaultContextMenuFactory.Instance)
        {
            _context = context;
            FastInitializeProperties();
            if (initializeMetadata)
            {
                RefreshMetadata();
            }
        }

        public void RefreshMetadata()
        {
            FetchProperty(nameof(Section));
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(Section));
        }

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
    }

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler { get; } = TaskScheduler.Current;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) => throw new AssertFailedException(ex.ToString());
    }
}
