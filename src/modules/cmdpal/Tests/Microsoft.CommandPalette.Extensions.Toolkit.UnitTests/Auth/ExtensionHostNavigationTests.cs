// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Auth;

[TestClass]
public class ExtensionHostNavigationTests
{
    // A host that implements only IExtensionHost (no IExtensionHost2), i.e. an
    // older Command Palette that predates host-driven navigation.
    private sealed class LegacyHost : IExtensionHost
    {
        public IAsyncAction ShowStatus(IStatusMessage message, StatusContext context) => null!;

        public IAsyncAction HideStatus(IStatusMessage message) => null!;

        public IAsyncAction LogMessage(ILogMessage message) => null!;
    }

    // A host that supports navigation and records what it was asked to do.
    private sealed class NavigatingHost : IExtensionHost2
    {
        public ICommand? LastPage { get; private set; }

        public NavigationMode LastMode { get; private set; }

        public int GoToPageCallCount { get; private set; }

        public IAsyncAction ShowStatus(IStatusMessage message, StatusContext context) => null!;

        public IAsyncAction HideStatus(IStatusMessage message) => null!;

        public IAsyncAction LogMessage(ILogMessage message) => null!;

        public IAsyncOperation<IAuthorizationResult> RequestAuthorizationAsync(IAuthorizationRequest request) => null!;

        public IAsyncAction GoToPageAsync(ICommand page, NavigationMode navigationMode)
        {
            LastPage = page;
            LastMode = navigationMode;
            GoToPageCallCount++;
            return Task.CompletedTask.AsAsyncAction();
        }
    }

    [TestMethod]
    public void SupportsNavigation_FalseForLegacyHost()
    {
        ExtensionHost.Initialize(new LegacyHost());

        Assert.IsFalse(ExtensionHost.SupportsNavigation);
    }

    [TestMethod]
    public void SupportsNavigation_TrueForHost2()
    {
        ExtensionHost.Initialize(new NavigatingHost());

        Assert.IsTrue(ExtensionHost.SupportsNavigation);
    }

    [TestMethod]
    public async Task GoToPageAsync_LegacyHost_ThrowsNotSupported()
    {
        ExtensionHost.Initialize(new LegacyHost());

        await Assert.ThrowsExceptionAsync<System.NotSupportedException>(
            () => ExtensionHost.GoToPageAsync(new Command()));
    }

    [TestMethod]
    public async Task GoToPageAsync_NullPage_ThrowsArgumentNull()
    {
        ExtensionHost.Initialize(new NavigatingHost());

        await Assert.ThrowsExceptionAsync<System.ArgumentNullException>(
            () => ExtensionHost.GoToPageAsync(null!));
    }

    [TestMethod]
    public async Task GoToPageAsync_ForwardsPageAndMode()
    {
        var host = new NavigatingHost();
        ExtensionHost.Initialize(host);

        var page = new Command();

        await ExtensionHost.GoToPageAsync(page, NavigationMode.GoHome);

        Assert.AreEqual(1, host.GoToPageCallCount);
        Assert.AreSame(page, host.LastPage);
        Assert.AreEqual(NavigationMode.GoHome, host.LastMode);
    }

    [TestMethod]
    public async Task GoToPageAsync_DefaultsToPush()
    {
        var host = new NavigatingHost();
        ExtensionHost.Initialize(host);

        await ExtensionHost.GoToPageAsync(new Command());

        Assert.AreEqual(NavigationMode.Push, host.LastMode);
    }
}
