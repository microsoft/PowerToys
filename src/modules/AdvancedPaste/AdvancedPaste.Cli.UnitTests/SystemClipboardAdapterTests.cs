// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

using FormsDataFormats = System.Windows.Forms.DataFormats;
using FormsDataObject = System.Windows.Forms.DataObject;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class SystemClipboardAdapterTests
{
    [TestMethod]
    public async Task DataObjectConversion_PreservesRichFormats()
    {
        const string text = "Hello PowerToys";
        var html = HtmlFormatHelper.CreateHtmlFormat("<strong>Hello PowerToys</strong>");
        var path = Path.GetTempFileName();
        var source = new FormsDataObject();
        source.SetText(text);
        source.SetData(FormsDataFormats.Html, autoConvert: false, html);
        source.SetFileDropList(new StringCollection { path, path + ".deleted" });
        using var image = new Bitmap(2, 2);
        source.SetImage(image);

        try
        {
            var packageView = SystemClipboardAdapter.CreateDataPackageView(source);
            var storageItems = await packageView.GetStorageItemsAsync();
            var roundTripped = await SystemClipboardAdapter.CreateDataObjectAsync(packageView);

            Assert.AreEqual(text, roundTripped.GetText());
            Assert.IsTrue(roundTripped.TryGetData<string>(FormsDataFormats.Html, out var roundTrippedHtml));
            Assert.AreEqual(html, roundTrippedHtml);
            Assert.AreEqual(path, storageItems.Single().Path);
            CollectionAssert.Contains(roundTripped.GetFileDropList(), path);
            Assert.IsNotNull(roundTripped.GetImage());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task RunOnSta_FromMta_ExecutesOnSta()
    {
        var apartment = await Task.Run(() => SystemClipboardAdapter.RunOnSta(() => Thread.CurrentThread.GetApartmentState()));

        Assert.AreEqual(ApartmentState.STA, apartment);
    }

    [TestMethod]
    public async Task RunOnSta_PropagatesOriginalException()
    {
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            Task.Run(() => SystemClipboardAdapter.RunOnSta<int>(() => throw new InvalidOperationException("Test failure"))));

        Assert.AreEqual("Test failure", exception.Message);
    }
}
