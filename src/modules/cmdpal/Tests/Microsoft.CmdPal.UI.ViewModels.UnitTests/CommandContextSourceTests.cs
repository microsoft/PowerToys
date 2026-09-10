// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class CommandContextSourceTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) =>
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void TopLevelAndRecentItems_PreserveHostAndProviderContext(int wrapperCount)
    {
        var host = new CommandPaletteHost(Mock.Of<IExtensionWrapper>());
        var provider = Mock.Of<ICommandProviderContext>(context => context.ProviderId == "test.provider" && context.SupportsPinning);
        var services = Mock.Of<IServiceProvider>(service => service.GetService(typeof(ISettingsService)) == Mock.Of<ISettingsService>());
        var pageContext = new TestPageContext();
        var page = new ListPage { Id = "test.page", Name = "Test page" };
        var model = new CommandItem(page);
        var itemViewModel = new CommandItemViewModel(new(model), new(pageContext), DefaultContextMenuFactory.Instance);
        itemViewModel.SlowInitializeProperties();
        var topLevel = new TopLevelViewModel(itemViewModel, TopLevelType.Normal, host, provider, new ProviderSettings(), services, model, DefaultContextMenuFactory.Instance);

        try
        {
            IListItem item = topLevel;
            for (var i = 0; i < wrapperCount; i++)
            {
                item = new RecentCommandListItem(item, "recorded-history-id");
            }

            var message = new PerformCommandMessage(new(item.Command), new ExtensionObject<IListItem>(item));
            var contextSource = message.Context as ICommandContextSource;

            Assert.IsNotNull(contextSource);
            Assert.AreSame(host, contextSource.ExtensionHost);
            Assert.AreSame(provider, contextSource.ProviderContext);
            Assert.AreSame(page, message.Command.Unsafe);
            Assert.AreSame(item, message.Context);
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void RecentItem_ForwardsAnyContextSource()
    {
        var host = new CommandPaletteHost(Mock.Of<IExtensionWrapper>());
        var provider = Mock.Of<ICommandProviderContext>();
        var source = new Mock<IListItem>();
        source.As<ICommandContextSource>().SetupGet(context => context.ExtensionHost).Returns(host);
        source.As<ICommandContextSource>().SetupGet(context => context.ProviderContext).Returns(provider);
        ICommandContextSource recent = new RecentCommandListItem(source.Object, "recorded-history-id");

        Assert.AreSame(host, recent.ExtensionHost);
        Assert.AreSame(provider, recent.ProviderContext);
    }

    [TestMethod]
    public void RecentItem_WithoutContextSource_ProvidesNoContext()
    {
        var app = new ListItem(new NoOpCommand());
        ICommandContextSource recent = new RecentCommandListItem(app, "app.id");

        Assert.IsNull(recent.ExtensionHost);
        Assert.IsNull(recent.ProviderContext);
    }
}
