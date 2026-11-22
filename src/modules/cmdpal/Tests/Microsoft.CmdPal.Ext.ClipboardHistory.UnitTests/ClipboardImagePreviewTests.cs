// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CmdPal.Ext.ClipboardHistory.Pages;
using Microsoft.CmdPal.Ext.ClipboardHistory.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public class ClipboardImagePreviewTests
{
    [TestMethod]
    [DataRow(1, 1, 1)]
    [DataRow(16, 16, 16)]
    [DataRow(48, 24, 24)]
    [DataRow(320, 240, 200)]
    public async Task CreateDetails_KeepsSmallImagesAtNativeSizeAndCapsHeight(int width, int height, int expectedMaxHeight)
    {
        using var image = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, image);
        var pixels = new byte[width * height * 4];
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();
        image.Seek(0);
        var imageData = RandomAccessStreamReference.CreateFromStream(image);

        var details = ClipboardListItem.CreateDetails(new ClipboardItem
        {
            Item = null!,
            Settings = null!,
            ImageData = imageData,
        });

        Assert.IsInstanceOfType<ImageContent>(details.Content.Single());
        var preview = (ImageContent)details.Content.Single();
        Assert.AreEqual(width, preview.MaxWidth);
        Assert.AreEqual(expectedMaxHeight, preview.MaxHeight);
        Assert.AreEqual(Resources.clipboard_item_image_title, details.Title);
        Assert.AreSame(imageData, preview.Image?.Light.Data);
        Assert.AreSame(imageData, preview.Image?.Dark.Data);
        Assert.AreEqual(string.Empty, preview.Image?.Light.Icon);
        Assert.AreEqual(string.Empty, details.Body);

        var dimensionDetails = details.Metadata.Single(entry => entry.Key == Resources.metadata_image_dimensions_key);
        Assert.IsInstanceOfType<DetailsLink>(dimensionDetails.Data);
        Assert.AreEqual($"{width} x {height}", ((DetailsLink)dimensionDetails.Data).Text);
    }

    [TestMethod]
    public void CreateDetails_TextUsesMarkdown()
    {
        var details = ClipboardListItem.CreateDetails(new ClipboardItem
        {
            Item = null!,
            Settings = null!,
            Content = "plain text",
        });

        Assert.AreEqual("```text\nplain text\n```", details.Body);
        Assert.HasCount(0, details.Content);
    }
}
