// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests.Auth;

[TestClass]
public sealed partial class AppExtensionHostNavigationTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    // A minimal page so the recipient can confirm the exact command was wrapped.
    private sealed partial class TestPage : Command
    {
    }

    [TestMethod]
    public async Task GoToPageAsync_Push_SendsPerformCommandWithShowWindow()
    {
        var recipient = new object();
        PerformCommandMessage? received = null;
        var homeOrBackSent = false;

        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(recipient, (_, m) => received = m);
        WeakReferenceMessenger.Default.Register<GoHomeMessage>(recipient, (_, _) => homeOrBackSent = true);
        WeakReferenceMessenger.Default.Register<GoBackMessage>(recipient, (_, _) => homeOrBackSent = true);

        try
        {
            var host = new TestAppExtensionHost();
            var page = new TestPage();

            await host.GoToPageAsync(page, NavigationMode.Push);

            Assert.IsNotNull(received, "PerformCommandMessage was not sent");
            Assert.IsTrue(received!.ShowWindowIfPage, "ShowWindowIfPage should be set");
            Assert.AreSame(page, received.Command.Unsafe, "the wrapped command should be the supplied page");
            Assert.IsFalse(homeOrBackSent, "Push must not reset the navigation stack");
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    [TestMethod]
    public async Task GoToPageAsync_GoHome_SendsGoHomeThenPerformCommand()
    {
        var recipient = new object();
        var goHomeSent = false;
        var goBackSent = false;
        var performSent = false;

        WeakReferenceMessenger.Default.Register<GoHomeMessage>(recipient, (_, _) => goHomeSent = true);
        WeakReferenceMessenger.Default.Register<GoBackMessage>(recipient, (_, _) => goBackSent = true);
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(recipient, (_, _) => performSent = true);

        try
        {
            var host = new TestAppExtensionHost();

            await host.GoToPageAsync(new TestPage(), NavigationMode.GoHome);

            Assert.IsTrue(goHomeSent, "GoHomeMessage should be sent for GoHome mode");
            Assert.IsFalse(goBackSent, "GoBackMessage should not be sent for GoHome mode");
            Assert.IsTrue(performSent, "PerformCommandMessage should still be sent");
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    [TestMethod]
    public async Task GoToPageAsync_GoBack_SendsGoBackThenPerformCommand()
    {
        var recipient = new object();
        var goHomeSent = false;
        var goBackSent = false;
        var performSent = false;

        WeakReferenceMessenger.Default.Register<GoHomeMessage>(recipient, (_, _) => goHomeSent = true);
        WeakReferenceMessenger.Default.Register<GoBackMessage>(recipient, (_, _) => goBackSent = true);
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(recipient, (_, _) => performSent = true);

        try
        {
            var host = new TestAppExtensionHost();

            await host.GoToPageAsync(new TestPage(), NavigationMode.GoBack);

            Assert.IsTrue(goBackSent, "GoBackMessage should be sent for GoBack mode");
            Assert.IsFalse(goHomeSent, "GoHomeMessage should not be sent for GoBack mode");
            Assert.IsTrue(performSent, "PerformCommandMessage should still be sent");
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    [TestMethod]
    public async Task GoToPageAsync_NullPage_IsGracefulNoOp()
    {
        var recipient = new object();
        var anySent = false;

        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(recipient, (_, _) => anySent = true);
        WeakReferenceMessenger.Default.Register<GoHomeMessage>(recipient, (_, _) => anySent = true);
        WeakReferenceMessenger.Default.Register<GoBackMessage>(recipient, (_, _) => anySent = true);

        try
        {
            var host = new TestAppExtensionHost();

            await host.GoToPageAsync(null, NavigationMode.Push);

            Assert.IsFalse(anySent, "a null page should not send any navigation message");
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }
}
