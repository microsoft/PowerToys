// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class TrayPaletteViewModelTests
{
    private sealed partial class TestProvider : CommandProvider
    {
        public TestProvider(string id)
        {
            Id = id;
        }

        public ICommandItem Item { get; } = new CommandItem(new AnonymousCommand(() => { }) { Id = "command" })
        {
            Title = "Quick action",
        };

        public override ICommandItem[] TopLevelCommands() => [];

        public override ICommandItem? GetCommandItem(string id) => id == "command" ? Item : null;
    }

    [TestMethod]
    public async Task ResolvesPinsInOrderAndPreservesProviderContext()
    {
        var first = new TestProvider("first");
        var second = new TestProvider("second");
        var settings = new SettingsModel
        {
            TrayPalette = new() { Commands = [new("second", "command"), new("missing", "command"), new("first", "missing"), new("first", "command")] },
        };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(() => settings);
        service.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));
        using var services = new ServiceCollection()
            .AddSingleton(service.Object)
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton<IContextMenuFactory>(DefaultContextMenuFactory.Instance)
            .BuildServiceProvider();
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([first, second], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);

        await model.RefreshAsync();

        Assert.HasCount(2, model.Items);
        Assert.AreEqual("second", model.Items[0].Pin.ProviderId);
        Assert.AreSame(second.Item, model.Items[0].Item.Model.Unsafe);
        Assert.AreEqual(model.Items[0].Pin, model.Items[0].PageContext.Pin);
        Assert.IsTrue(model.Items[0].Item.PageContext.TryGetTarget(out var context));
        Assert.AreSame(model.Items[0].PageContext, context);
        Assert.AreSame(first.Item, model.Items[1].Item.Model.Unsafe);
        Assert.AreEqual("second", model.Items[0].CreateMessage().SourceProviderContext?.ProviderId);
        Assert.IsNotNull(model.Items[0].CreateMessage().SourceExtensionHost);
        Assert.IsFalse(model.HasLoadError);
        Assert.IsFalse(model.IsEmpty);
        Assert.IsFalse(model.IsLoading);

        var original = model.Items[0];
        var changes = 0;
        model.Items.CollectionChanged += (_, _) => changes++;
        await model.RefreshAsync();
        Assert.AreSame(original, model.Items[0]);
        Assert.AreEqual(0, changes, "Reopening must not reset the grid or recreate its tiles.");

        manager.TopLevelCommands.Clear();
        await model.RefreshAsync();
        Assert.AreNotSame(original, model.Items[0], "Provider command changes must invalidate cached tiles.");

        model.Items.Move(1, 0);
        model.SaveOrder();
        Assert.AreEqual("first", settings.TrayPalette.Commands[0].ProviderId);
        Assert.AreEqual("missing", settings.TrayPalette.Commands[1].ProviderId);
        Assert.AreEqual("second", settings.TrayPalette.Commands[3].ProviderId);

        await model.RefreshAsync();
        Assert.AreEqual("first", model.Items[0].Pin.ProviderId);
        settings = settings with { TrayPalette = settings.TrayPalette.Unpin(model.Items[0].Pin) };
        await model.RefreshAsync();
        Assert.HasCount(1, model.Items);
        Assert.AreEqual("second", model.Items[0].Pin.ProviderId);
    }

    [TestMethod]
    public async Task DisabledProviderDoesNotResolvePins()
    {
        var provider = new TestProvider("disabled");
        var settings = new SettingsModel
        {
            TrayPalette = new() { Commands = [new("disabled", "command")] },
        };
        settings = settings with
        {
            ProviderSettings = settings.ProviderSettings.SetItem("disabled", new() { IsEnabled = false }),
        };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(settings);
        using var services = new ServiceCollection()
            .AddSingleton(service.Object)
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton<IContextMenuFactory>(DefaultContextMenuFactory.Instance)
            .BuildServiceProvider();
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);

        await model.RefreshAsync();

        Assert.IsEmpty(model.Items);
        Assert.IsTrue(model.IsEmpty);
        Assert.IsFalse(model.HasLoadError);
        Assert.HasCount(1, settings.TrayPalette.Commands);
    }
}
