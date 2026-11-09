// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class ProviderFallbackSettingsTests
{
    [TestMethod]
    public async Task SmallContent_AdvertisedIdPassesAdvisorySizeToProvider()
    {
        await using var services = CreateServices(new FakeSettingsService());
        var provider = new SmallContentProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        await wrapper.LoadTopLevelCommands(services);
        var size = new Size(360, 180);

        Assert.AreSame(provider.Content, wrapper.GetSmallContent("small", size));
        Assert.AreEqual(size, provider.LastSize);
        Assert.AreEqual(1, provider.ContentRequests);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("SMALL")]
    [DataRow("")]
    public async Task SmallContent_UnadvertisedIdsUseButtonFallback(string id)
    {
        await using var services = CreateServices(new FakeSettingsService());
        var provider = new SmallContentProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        await wrapper.LoadTopLevelCommands(services);

        Assert.IsNull(wrapper.GetSmallContent(id, new Size(360, 180)));
        Assert.AreEqual(0, provider.ContentRequests);
    }

    [TestMethod]
    public void SmallContent_InactiveProviderIsNotCalled()
    {
        var provider = new SmallContentProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);

        Assert.IsNull(wrapper.GetSmallContent("small", new Size(360, 180)));
        Assert.AreEqual(0, provider.ContentRequests);
    }

    [TestMethod]
    public async Task SmallContent_NullAndFailuresAreNotConvertedToSuccessfulContent()
    {
        await using var services = CreateServices(new FakeSettingsService());
        var provider = new SmallContentProvider { Content = null };
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        await wrapper.LoadTopLevelCommands(services);

        Assert.IsNull(wrapper.GetSmallContent("small", new Size(360, 180)));
        provider.ThrowOnRequest = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => wrapper.GetSmallContent("small", new Size(360, 180)));
    }

    private sealed partial class SmallContentProvider : CommandProvider
    {
        public IContent? Content { get; set; } = new FormContent();

        public int ContentRequests { get; private set; }

        public Size LastSize { get; private set; }

        public bool ThrowOnRequest { get; set; }

        public SmallContentProvider()
        {
            Id = "small-content-test";
            DisplayName = "Small content";
        }

        public override ICommandItem[] TopLevelCommands() => [Item("small")];

        public override ICommandItem[] GetDockBands() => [Item("band")];

        public override ICommandItem? GetCommandItem(string id) => id == "nested" ? Item(id) : null;

        public override string[] GetAvailableSmallContent() => ["small"];

        public override IContent? GetSmallContent(string commandId, Size availableSpace)
        {
            ContentRequests++;
            LastSize = availableSpace;
            return ThrowOnRequest ? throw new InvalidOperationException("Provider failure") : Content;
        }

        private static CommandItem Item(string id) => new(new NoOpCommand { Id = id }) { Title = id };
    }
}
