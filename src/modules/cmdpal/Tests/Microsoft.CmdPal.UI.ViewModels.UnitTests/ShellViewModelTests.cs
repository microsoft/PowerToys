// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[DoNotParallelize]
public partial class ShellViewModelTests
{
    private SynchronizationContext? _originalSynchronizationContext;

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    {
        {
        }
    }

    {
    }

    {
        {
        }
    }

    [TestMethod]
    {

        var shell = new ShellViewModel(
            TaskScheduler.Default,
            rootPageService.Object,
            pageViewModelFactory.Object,
            appHostService.Object);
        var windowMessageRecipient = new object();
        var showWindowCount = 0;
        WeakReferenceMessenger.Default.Register<ShowWindowMessage>(
            windowMessageRecipient,
            (_, _) => showWindowCount++);

        try
        {
            {
            };
            {
            };
            viewModel.Receive(new PerformCommandMessage(new ExtensionObject<ICommand>(command)));


        }
        finally
        {
        }
    }

    [TestMethod]
    {

        try
        {
                {


            }
            finally
            {
        }
    }

    [TestMethod]
    {

        try
        {

        }
        finally
        {
    }

    private static ShellViewModel CreateViewModel()
    {
        var host = new TestAppExtensionHost();
        var appHostService = new Mock<IAppHostService>();
        appHostService.Setup(service => service.GetDefaultHost()).Returns(host);
        appHostService.Setup(service => service.GetHostForCommand(It.IsAny<object?>(), It.IsAny<AppExtensionHost?>())).Returns(host);
        appHostService.Setup(service => service.GetProviderContextForCommand(It.IsAny<object?>(), It.IsAny<ICommandProviderContext?>())).Returns(CommandProviderContext.Empty);

        var pageFactory = new Mock<IPageViewModelFactoryService>();
        pageFactory.Setup(factory => factory.TryCreatePageViewModel(It.IsAny<IPage>(), It.IsAny<bool>(), It.IsAny<AppExtensionHost>(), It.IsAny<ICommandProviderContext>()))
            .Returns(new TestPageViewModel(host));

        return new ShellViewModel(
            TaskScheduler.Default,
            Mock.Of<IRootPageService>(),
            pageFactory.Object,
            appHostService.Object);
    }
}
