// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AdvancedPaste.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class SystemClipboardAdapterTests
{
    [TestMethod]
    public async Task ClipboardSnapshotConversion_PreservesRichFormats()
    {
        const string text = "Hello PowerToys \u03a9";
        var html = HtmlFormatHelper.CreateHtmlFormat($"<strong>{text}</strong>");
        var folder = Directory.CreateTempSubdirectory();
        var path = Path.Combine(folder.FullName, "file.txt");

        try
        {
            File.WriteAllText(path, text);
            var bitmapBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAEElEQVR4AQEFAPr/AP8AAP8FAAH/+lyI0QAAAABJRU5ErkJggg==");
            var source = new ClipboardSnapshot(text, html, [path, path + ".deleted", folder.FullName], bitmapBytes);
            var packageView = SystemClipboardAdapter.CreateDataPackageView(source);
            var storageItems = await packageView.GetStorageItemsAsync();
            var roundTripped = await SystemClipboardAdapter.CreateClipboardSnapshotAsync(packageView);

            Assert.AreEqual(text, roundTripped.Text);
            Assert.AreEqual(html, roundTripped.Html);
            var expectedPaths = new[] { path, folder.FullName };
            CollectionAssert.AreEqual(expectedPaths, storageItems.Select(item => item.Path).ToArray());
            CollectionAssert.AreEqual(expectedPaths, roundTripped.FilePaths.ToArray());
            CollectionAssert.AreEqual(bitmapBytes, roundTripped.BitmapBytes);
        }
        finally
        {
            File.Delete(path);
            folder.Delete();
        }
    }

    [TestMethod]
    public async Task ClipboardSnapshotConversion_OmitsMissingFormats()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var packageView = SystemClipboardAdapter.CreateDataPackageView(new ClipboardSnapshot(null, null, [missingPath], null));
        var roundTripped = await SystemClipboardAdapter.CreateClipboardSnapshotAsync(packageView);

        Assert.IsFalse(packageView.Contains(StandardDataFormats.StorageItems));
        Assert.IsNull(roundTripped.Text);
        Assert.IsNull(roundTripped.Html);
        Assert.AreEqual(0, roundTripped.FilePaths.Count);
        Assert.IsNull(roundTripped.BitmapBytes);
    }
}
