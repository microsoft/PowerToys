// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class PageViewModelLifecycleTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private class TrackingPageViewModel : PageViewModel
    {
        public int CleanupCount { get; private set; }

        public TrackingPageViewModel()
            : base(null, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty)
        {
        }

        protected override void UnsafeCleanup()
        {
            CleanupCount++;
            base.UnsafeCleanup();
        }
    }

    private sealed class BlockingPageViewModel : TrackingPageViewModel
    {
        public ManualResetEventSlim InitializationStarted { get; } = new();

        public ManualResetEventSlim ContinueInitialization { get; } = new();

        public override void InitializeProperties()
        {
            InitializationStarted.Set();
            Assert.IsTrue(ContinueInitialization.Wait(TimeSpan.FromSeconds(5)));
        }
    }

    [TestMethod]
    public void CleanupIfTransient_OnlyCleansDiscardedTransientPages()
    {
        var rootPage = new TrackingPageViewModel();
        var transientPage = new TrackingPageViewModel { IsTransientPage = true };
        var nestedPage = new TrackingPageViewModel();

        rootPage.SafeCleanupIfTransient();
        transientPage.SafeCleanupIfTransient();
        nestedPage.SafeCleanupIfTransient();
        transientPage.SafeCleanupIfTransient();

        Assert.AreEqual(0, rootPage.CleanupCount);
        Assert.AreEqual(1, transientPage.CleanupCount);
        Assert.AreEqual(0, nestedPage.CleanupCount);

        rootPage.SafeCleanup();
        nestedPage.SafeCleanup();
    }

    [TestMethod]
    public async Task CleanupDuringInitialization_IsDeferredUntilInitializationCompletes()
    {
        var page = new BlockingPageViewModel();
        var initialization = Task.Run(page.InitializeAsync);

        Assert.IsTrue(page.InitializationStarted.Wait(TimeSpan.FromSeconds(5)));

        page.SafeCleanup();
        Assert.AreEqual(0, page.CleanupCount);

        page.ContinueInitialization.Set();
        await initialization;

        Assert.AreEqual(1, page.CleanupCount);
        page.SafeCleanup();
        Assert.AreEqual(1, page.CleanupCount);
    }
}
