// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class ParametersPageViewModelTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    /// <summary>
    /// A parameters page with no parameter runs - these tests are about the
    /// page's own Command, not its parameters.
    /// </summary>
    private sealed partial class TestParametersPage : Page, IParametersPage
    {
        public IParameterRun[] Parameters { get; set; } = [];

        public IListItem Command { get; set => SetProperty(ref field, value); } = null!;
    }

    /// <summary>
    /// A label run that reports whether anything is still subscribed, and can
    /// fail the way a dying extension would once a view-model has attached.
    /// </summary>
    private sealed partial class CountingLabelRun : ILabelRun
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add => _propChanged += value;
            remove => _propChanged -= value;
        }

        public int HandlerCount => _propChanged?.GetInvocationList().Length ?? 0;

        public bool ThrowOnText { get; init; }

        public string Text => ThrowOnText
            ? throw new InvalidOperationException("Extension went away")
            : "label";
    }

    [TestMethod]
    public void FetchItems_ThatThrows_ReleasesPartiallyBuiltItems()
    {
        // LabelRunViewModel subscribes in base.InitializeProperties and only
        // then reads Text, so the failing run has already attached by the time
        // it throws - both it and the run before it have to come off.
        var host = new TestAppExtensionHost();
        var good = new CountingLabelRun();
        var bad = new CountingLabelRun { ThrowOnText = true };
        var page = new TestParametersPage
        {
            Command = new ListItem(new NoOpCommand { Name = "Run it" }) { Title = "Go" },
            Parameters = [good, bad],
        };

        var vm = CreateViewModel(page, host);
        vm.InitializeProperties();

        Assert.AreEqual(0, good.HandlerCount, "an item built before the failure was left subscribed");
        Assert.AreEqual(0, bad.HandlerCount, "the item that failed was left subscribed");
    }

    [TestMethod]
    public void Cleanup_ReleasesCommandItemViewModel()
    {
        // The extension-side list item is the root: CommandItemViewModel
        // subscribes to its PropChanged when it initializes.
        var host = new TestAppExtensionHost();
        var item = new ListItem(new NoOpCommand { Name = "Run it" }) { Title = "Go" };
        var page = new TestParametersPage { Command = item };

        var weakCommandVm = InitializeAndCleanup(page, host);

        AssertCollected(weakCommandVm, "CommandItemViewModel from the parameters page");

        GC.KeepAlive(item);
        GC.KeepAlive(page);
        GC.KeepAlive(host);
    }

    [TestMethod]
    public void ReplaceCommand_ReleasesDisplacedCommandItemViewModel()
    {
        var host = new TestAppExtensionHost();
        var item = new ListItem(new NoOpCommand { Name = "Run it" }) { Title = "Go" };
        var page = new TestParametersPage { Command = item };

        var weakDisplacedVm = InitializeTwice(page, host);

        AssertCollected(weakDisplacedVm, "CommandItemViewModel displaced by a rebuild");

        GC.KeepAlive(item);
        GC.KeepAlive(page);
        GC.KeepAlive(host);
    }

    // Separate frames so the view-models are unreachable on return - a Debug
    // build keeps locals alive to the end of their scope.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<CommandItemViewModel> InitializeAndCleanup(IParametersPage page, AppExtensionHost host)
    {
        var vm = CreateViewModel(page, host);
        vm.InitializeProperties();

        var weak = new WeakReference<CommandItemViewModel>(vm.Command);

        vm.SafeCleanup();
        vm.Dispose();
        return weak;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<CommandItemViewModel> InitializeTwice(IParametersPage page, AppExtensionHost host)
    {
        var vm = CreateViewModel(page, host);
        vm.InitializeProperties();

        var weak = new WeakReference<CommandItemViewModel>(vm.Command);

        // Rebuilds Command, which must release the instance it displaces.
        vm.InitializeProperties();

        vm.SafeCleanup();
        vm.Dispose();
        return weak;
    }

    private static void AssertCollected<T>(WeakReference<T> reference, string what)
        where T : class
    {
        // BatchUpdateManager holds queued targets until its timer drains.
        Thread.Sleep(200);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

        Assert.IsFalse(reference.TryGetTarget(out _), $"{what} is still reachable after cleanup.");
    }

    private static ParametersPageViewModel CreateViewModel(IParametersPage page, AppExtensionHost host) =>
        new(page, TaskScheduler.Default, host, CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
}
