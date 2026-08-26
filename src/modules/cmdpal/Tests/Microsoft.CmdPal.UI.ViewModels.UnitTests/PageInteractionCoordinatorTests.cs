// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class PageInteractionCoordinatorTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed class TestCommandBar : ICommandBarInteractionTarget
    {
        public List<ICommandBarContext?> Contexts { get; } = [];

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public int KeybindingCount { get; private set; }

        public bool KeybindingResult { get; set; }

        public void SetCommandContext(ICommandBarContext? context) => Contexts.Add(context);

        public void OpenContextMenu() => OpenCount++;

        public void CloseContextMenu() => CloseCount++;

        public bool TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
        {
            KeybindingCount++;
            return KeybindingResult;
        }
    }

    private sealed class TestPageTarget : IPageInteractionTarget, IPageInteractionEventSource
    {
        public event EventHandler? ContextMenuCloseRequested;

        public event EventHandler? FocusSearchRequested;

        public event EventHandler<PageDragStateChangedEventArgs>? DragStateChanged;

        public int PreviousCount { get; private set; }

        public int NextCount { get; private set; }

        public int LeftCount { get; private set; }

        public int RightCount { get; private set; }

        public int PageUpCount { get; private set; }

        public int PageDownCount { get; private set; }

        public int PrimaryCount { get; private set; }

        public int SecondaryCount { get; private set; }

        public void NavigatePrevious() => PreviousCount++;

        public void NavigateNext() => NextCount++;

        public void NavigateLeft() => LeftCount++;

        public void NavigateRight() => RightCount++;

        public void NavigatePageUp() => PageUpCount++;

        public void NavigatePageDown() => PageDownCount++;

        public void ActivatePrimary() => PrimaryCount++;

        public void ActivateSecondary() => SecondaryCount++;

        public void RequestContextMenuClose() => ContextMenuCloseRequested?.Invoke(this, EventArgs.Empty);

        public void RequestSearchFocus() => FocusSearchRequested?.Invoke(this, EventArgs.Empty);

        public void SetDragging(bool isDragging) => DragStateChanged?.Invoke(this, new(isDragging));
    }

    [TestMethod]
    public void TwoPageHosts_UpdateOnlyTheirOwningCommandBar()
    {
        var pageA = CreatePage();
        var pageB = CreatePage();
        var barA = new TestCommandBar();
        var barB = new TestCommandBar();
        using var hostA = new PageInteractionCoordinator(barA);
        using var hostB = new PageInteractionCoordinator(barB);
        hostA.AttachPage(pageA);
        hostB.AttachPage(pageB);
        barA.Contexts.Clear();
        barB.Contexts.Clear();
        var contextA = Mock.Of<ICommandBarContext>();
        var contextB = Mock.Of<ICommandBarContext>();

        pageA.SetCommandBarContext(contextA);
        pageB.SetCommandBarContext(contextB);

        CollectionAssert.AreEqual(new[] { contextA }, barA.Contexts);
        CollectionAssert.AreEqual(new[] { contextB }, barB.Contexts);
    }

    [TestMethod]
    public void SuggestionsAndParameterFocus_StayWithTheirOwningPage()
    {
        var pageA = CreatePage();
        var pageB = CreatePage();
        using var hostA = new PageInteractionCoordinator(new TestCommandBar());
        using var hostB = new PageInteractionCoordinator(new TestCommandBar());
        hostA.AttachPage(pageA);
        hostB.AttachPage(pageB);
        var suggestionsA = new List<string>();
        var suggestionsB = new List<string>();
        var focusA = 0;
        var focusB = 0;
        hostA.SearchSuggestionChanged += (_, e) => suggestionsA.Add(e.Suggestion);
        hostB.SearchSuggestionChanged += (_, e) => suggestionsB.Add(e.Suggestion);
        hostA.ParameterFocusRequested += (_, _) => focusA++;
        hostB.ParameterFocusRequested += (_, _) => focusB++;

        pageA.SetSearchSuggestion("alpha");
        pageA.RequestParameterFocus(null!);

        Assert.AreEqual(1, suggestionsA.Count);
        Assert.AreEqual("alpha", suggestionsA[0]);
        Assert.AreEqual(1, focusA);
        Assert.AreEqual(0, suggestionsB.Count);
        Assert.AreEqual(0, focusB);
    }

    [TestMethod]
    public void KeyboardNavigationAndActivation_ReachOnlyTheActivePage()
    {
        using var host = new PageInteractionCoordinator(new TestCommandBar());
        var oldTarget = new TestPageTarget();
        var activeTarget = new TestPageTarget();
        host.AttachTarget(oldTarget);
        host.AttachTarget(activeTarget);

        host.NavigatePrevious();
        host.NavigateNext();
        host.NavigateLeft();
        host.NavigateRight();
        host.NavigatePageUp();
        host.NavigatePageDown();
        host.ActivatePrimary();
        host.ActivateSecondary();

        Assert.AreEqual(0, oldTarget.NextCount);
        Assert.AreEqual(1, activeTarget.PreviousCount);
        Assert.AreEqual(1, activeTarget.NextCount);
        Assert.AreEqual(1, activeTarget.LeftCount);
        Assert.AreEqual(1, activeTarget.RightCount);
        Assert.AreEqual(1, activeTarget.PageUpCount);
        Assert.AreEqual(1, activeTarget.PageDownCount);
        Assert.AreEqual(1, activeTarget.PrimaryCount);
        Assert.AreEqual(1, activeTarget.SecondaryCount);
    }

    [TestMethod]
    public void ContextMenuOperations_ReachOnlyTheOwningCommandBar()
    {
        var barA = new TestCommandBar { KeybindingResult = true };
        var barB = new TestCommandBar();
        using var hostA = new PageInteractionCoordinator(barA);
        using var hostB = new PageInteractionCoordinator(barB);
        var targetA = new TestPageTarget();
        var targetB = new TestPageTarget();
        hostA.AttachTarget(targetA);
        hostB.AttachTarget(targetB);

        hostA.OpenContextMenu();
        targetA.RequestContextMenuClose();
        Assert.IsTrue(hostA.TryCommandKeybinding(true, false, false, false, VirtualKey.K));

        Assert.AreEqual(1, barA.OpenCount);
        Assert.AreEqual(1, barA.CloseCount);
        Assert.AreEqual(1, barA.KeybindingCount);
        Assert.AreEqual(0, barB.OpenCount);
        Assert.AreEqual(0, barB.CloseCount);
        Assert.AreEqual(0, barB.KeybindingCount);
    }

    [TestMethod]
    public void StalePageCallbacks_AreIgnoredAfterNavigation()
    {
        var oldPage = CreatePage();
        var currentPage = CreatePage();
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        var suggestions = new List<string>();
        var detailsChanges = 0;
        host.SearchSuggestionChanged += (_, e) => suggestions.Add(e.Suggestion);
        host.DetailsChanged += (_, _) => detailsChanges++;
        host.AttachPage(oldPage);
        host.AttachPage(currentPage);
        bar.Contexts.Clear();
        suggestions.Clear();
        detailsChanges = 0;

        oldPage.SetCommandBarContext(Mock.Of<ICommandBarContext>());
        oldPage.SetSearchSuggestion("stale");
        oldPage.SetDetails(null);

        Assert.AreEqual(0, bar.Contexts.Count);
        Assert.AreEqual(0, suggestions.Count);
        Assert.AreEqual(0, detailsChanges);
    }

    [TestMethod]
    public void DragState_AffectsOnlyTheOwningHost()
    {
        using var hostA = new PageInteractionCoordinator(new TestCommandBar());
        using var hostB = new PageInteractionCoordinator(new TestCommandBar());
        var targetA = new TestPageTarget();
        var targetB = new TestPageTarget();
        hostA.AttachTarget(targetA);
        hostB.AttachTarget(targetB);
        var dragA = new List<bool>();
        var dragB = new List<bool>();
        hostA.DragStateChanged += (_, e) => dragA.Add(e.IsDragging);
        hostB.DragStateChanged += (_, e) => dragB.Add(e.IsDragging);

        targetA.SetDragging(true);
        targetA.SetDragging(false);

        Assert.AreEqual(2, dragA.Count);
        Assert.IsTrue(dragA[0]);
        Assert.IsFalse(dragA[1]);
        Assert.AreEqual(0, dragB.Count);
    }

    [TestMethod]
    public void AttachDetach_DoesNotDuplicateHandlersOrRetainOldPages()
    {
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        var page = CreatePage();
        host.AttachPage(page);
        host.AttachPage(page);
        bar.Contexts.Clear();

        page.SetCommandBarContext(Mock.Of<ICommandBarContext>());

        Assert.AreEqual(1, bar.Contexts.Count);

        var oldPageReference = AttachAndReplacePage(host);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(oldPageReference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachAndReplacePage(PageInteractionCoordinator host)
    {
        var oldPage = CreatePage();
        host.AttachPage(oldPage);
        var reference = new WeakReference(oldPage);
        host.AttachPage(CreatePage());
        return reference;
    }

    private static PageViewModel CreatePage() =>
        new(new Page(), TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);
}
