// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class DockDropTargetTests
{
    [TestMethod]
    [DataRow(DockSide.Top, 0, DockPinSide.Start)]
    [DataRow(DockSide.Top, 99, DockPinSide.Start)]
    [DataRow(DockSide.Top, 100, DockPinSide.Center)]
    [DataRow(DockSide.Top, 199, DockPinSide.Center)]
    [DataRow(DockSide.Top, 200, DockPinSide.End)]
    [DataRow(DockSide.Top, 299, DockPinSide.End)]
    [DataRow(DockSide.Bottom, 50, DockPinSide.Start)]
    [DataRow(DockSide.Bottom, 150, DockPinSide.Center)]
    [DataRow(DockSide.Bottom, 250, DockPinSide.End)]
    public void HorizontalDock_UsesThreeEqualZones(DockSide dockSide, int x, DockPinSide expected)
    {
        var target = Resolve(CreateUriData(), dockSide, new Point(x, 16), new Size(300, 32));

        Assert.IsNotNull(target);
        Assert.AreEqual(expected, target.Value.Side);
    }

    [TestMethod]
    [DataRow(DockSide.Left, 0, DockPinSide.Start)]
    [DataRow(DockSide.Left, 99, DockPinSide.Start)]
    [DataRow(DockSide.Left, 100, DockPinSide.Center)]
    [DataRow(DockSide.Left, 199, DockPinSide.Center)]
    [DataRow(DockSide.Left, 200, DockPinSide.End)]
    [DataRow(DockSide.Left, 299, DockPinSide.End)]
    [DataRow(DockSide.Right, 50, DockPinSide.Start)]
    [DataRow(DockSide.Right, 150, DockPinSide.Center)]
    [DataRow(DockSide.Right, 250, DockPinSide.End)]
    public void VerticalDock_UsesThreeEqualZones(DockSide dockSide, int y, DockPinSide expected)
    {
        var target = Resolve(CreateUriData(), dockSide, new Point(16, y), new Size(48, 300));

        Assert.IsNotNull(target);
        Assert.AreEqual(expected, target.Value.Side);
    }

    [TestMethod]
    [DataRow(-1, 16, 300, 32)]
    [DataRow(300, 16, 300, 32)]
    [DataRow(150, -1, 300, 32)]
    [DataRow(150, 32, 300, 32)]
    [DataRow(0, 0, 0, 32)]
    [DataRow(0, 0, 300, 0)]
    public void OutsideDockOrEmptyLayout_HasNoTarget(int x, int y, int width, int height)
    {
        Assert.IsNull(Resolve(CreateUriData(), DockSide.Top, new Point(x, y), new Size(width, height)));
    }

    [TestMethod]
    public void EditMode_DoesNotAcceptExternalPinning()
    {
        Assert.IsNull(Resolve(CreateUriData(), isEditMode: true));
    }

    [TestMethod]
    public void BandDrag_DoesNotBecomeABookmarkDrop()
    {
        var data = CreateUriData();
        data.Properties["DockBandId"] = "band-id";

        Assert.IsNull(Resolve(data));
    }

    [TestMethod]
    public void UnsupportedData_HasNoTarget()
    {
        var data = new DataPackage();
        data.SetText("Text cannot be pinned by dropping it on the dock.");

        Assert.IsNull(Resolve(data));
    }

    [TestMethod]
    public void StorageItems_AreAcceptedWithoutReadingTheirContents()
    {
        var data = new DataPackage();
        data.SetDataProvider(StandardDataFormats.StorageItems, _ => Assert.Fail("Drag-over must not read files."));

        Assert.IsNotNull(Resolve(data));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ModernLinks_AreAcceptedWithoutReadingTheirContents(bool applicationLink)
    {
        var data = new DataPackage();
        var format = applicationLink ? StandardDataFormats.ApplicationLink : StandardDataFormats.WebLink;
        data.SetDataProvider(format, _ => Assert.Fail("Drag-over must not read links."));

        Assert.IsNotNull(Resolve(data));
        Assert.IsNull(Resolve(data, isEditMode: true));

        data.Properties["DockBandId"] = "band-id";
        Assert.IsNull(Resolve(data));
    }

    [TestMethod]
    public async Task ApplicationLink_IsAcceptedAndRead()
    {
        var uri = new Uri("ms-settings:display");
        var data = new DataPackage();
        data.SetApplicationLink(uri);

        Assert.IsNotNull(Resolve(data));
        Assert.AreEqual(uri, await DockDropTarget.ReadLinkAsync(data.GetView()));
    }

    [TestMethod]
    public async Task WebLink_IsAcceptedAndRead()
    {
        var uri = new Uri("https://example.com/web-link");
        var data = new DataPackage();
        data.SetWebLink(uri);

        Assert.IsNotNull(Resolve(data));
        Assert.AreEqual(uri, await DockDropTarget.ReadLinkAsync(data.GetView()));
    }

    [TestMethod]
    public async Task LegacyUri_IsStillRead()
    {
        var uri = new Uri("https://example.com/legacy-uri");
        var data = new DataPackage();
        data.SetData(StandardDataFormats.Uri, uri);

        Assert.AreEqual(uri, await DockDropTarget.ReadLinkAsync(data.GetView()));
    }

    [TestMethod]
    public async Task MultipleLinkFormats_PreferApplicationLink()
    {
        var uri = new Uri("ms-settings:display");
        var data = new DataPackage();
        data.SetApplicationLink(uri);
        data.SetWebLink(new Uri("https://example.com/web-link"));

        Assert.AreEqual(uri, await DockDropTarget.ReadLinkAsync(data.GetView()));
    }

    [TestMethod]
    public async Task NoLinkFormat_ReturnsNull()
    {
        var data = new DataPackage();
        data.SetText("Plain text does not supply a link.");

        Assert.IsNull(await DockDropTarget.ReadLinkAsync(data.GetView()));
    }

    [TestMethod]
    [DataRow(0, "monitor-id", DockPinSide.Center, null)]
    [DataRow(1, "monitor-id", DockPinSide.Start, null)]
    [DataRow(1, "monitor-id", DockPinSide.Center, null)]
    [DataRow(1, "monitor-id", DockPinSide.End, null)]
    [DataRow(2, "secondary-monitor", DockPinSide.Start, "secondary-monitor")]
    [DataRow(2, "secondary-monitor", DockPinSide.Center, "secondary-monitor")]
    [DataRow(2, "secondary-monitor", DockPinSide.End, "secondary-monitor")]
    [DataRow(3, "secondary-monitor", DockPinSide.Center, "secondary-monitor")]
    [DataRow(2, null, DockPinSide.Center, null)]
    public void PinMessage_UsesSharedOrMonitorDestination(int monitorCount, string? monitorDeviceId, DockPinSide side, string? expectedMonitorDeviceId)
    {
        var x = side switch
        {
            DockPinSide.Start => 50,
            DockPinSide.Center => 150,
            _ => 250,
        };
        var target = DockDropTarget.Resolve(
            CreateUriData().GetView(),
            false,
            DockSide.Bottom,
            new Point(x, 16),
            new Size(300, 32),
            monitorDeviceId,
            monitorCount);

        Assert.IsNotNull(target);
        var message = target.Value.CreatePinMessage("Bookmarks", "bookmark-command");

        Assert.AreEqual("Bookmarks", message.ProviderId);
        Assert.AreEqual("bookmark-command", message.CommandId);
        Assert.AreEqual(expectedMonitorDeviceId, message.MonitorDeviceId);
        Assert.AreEqual(side, message.Side);
        Assert.IsTrue(message.Pin);
        Assert.IsFalse(message.WithReload);
    }

    private static DataPackage CreateUriData()
    {
        var data = new DataPackage();
        data.SetUri(new Uri("https://example.com"));
        return data;
    }

    private static DockDropTarget? Resolve(
        DataPackage data,
        DockSide side = DockSide.Top,
        Point? position = null,
        Size? size = null,
        bool isEditMode = false) =>
        DockDropTarget.Resolve(data.GetView(), isEditMode, side, position ?? new Point(150, 16), size ?? new Size(300, 32), "monitor-id", 2);
}
