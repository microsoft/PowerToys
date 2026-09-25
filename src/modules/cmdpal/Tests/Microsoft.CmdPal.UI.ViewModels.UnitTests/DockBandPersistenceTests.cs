// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockBandPersistenceTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) => throw new AssertFailedException(ex.Message);
    }

    [TestMethod]
    public void SaveShowLabels_PersistsTaskbarBandSettings()
    {
        var bandSettings = new DockBandSettings { ProviderId = "test", CommandId = "test.band" };
        var settings = new SettingsModel
        {
            DockSettings = new DockSettings
            {
                TaskbarBands = ImmutableList.Create(bandSettings),
                StartBands = ImmutableList<DockBandSettings>.Empty,
                CenterBands = ImmutableList<DockBandSettings>.Empty,
                EndBands = ImmutableList<DockBandSettings>.Empty,
            },
        };
        var service = new Mock<ISettingsService>();
        service.Setup(s => s.Settings).Returns(() => settings);
        service.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), false))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));

        var pageContext = new TestPageContext();
        var command = new CommandItem(new NoOpCommand { Name = "Test" });
        var item = new CommandItemViewModel(new(command), new(pageContext), DefaultContextMenuFactory.Instance);
        var band = new DockBandViewModel(item, new(pageContext), bandSettings, service.Object, DefaultContextMenuFactory.Instance);

        band.SnapshotShowLabels();
        band.ShowTitles = false;
        band.ShowSubtitles = false;
        band.SaveShowLabels();

        Assert.IsFalse(settings.DockSettings.TaskbarBands[0].ShowTitles);
        Assert.IsFalse(settings.DockSettings.TaskbarBands[0].ShowSubtitles);
        service.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), false), Times.Once);
    }
}
