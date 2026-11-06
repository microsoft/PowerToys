// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class PasteFormatExecutorTests
{
    [TestMethod]
    public async Task Markdown_TransformsHtmlOnlyInputThroughSharedExecutor()
    {
        var package = new DataPackage();
        package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat("<p><strong>HTML-only text</strong></p>"));
        var format = PasteFormat.CreateStandardFormat(PasteFormats.Markdown, ClipboardFormat.Html, true, value => value);
        var executor = new PasteFormatExecutor(
            Mock.Of<IKernelService>(),
            Mock.Of<ICustomActionTransformService>(),
            Mock.Of<IUserSettings>());

        var output = await executor.ExecutePasteFormatAsync(format, package.GetView(), PasteActionSource.CommandLine, CancellationToken.None, null);

        Assert.IsTrue(format.IsEnabled);
        StringAssert.Contains(await output.GetView().GetTextAsync(), "**HTML-only text**");
    }

    [TestMethod]
    public async Task CustomAction_TransformsHtmlOnlyTextWithoutAnImage()
    {
        var package = new DataPackage();
        package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat("<p>HTML-only text</p>"));
        var format = PasteFormat.CreateCustomAIFormat(PasteFormats.CustomTextTransformation, "action", "prompt", true, ClipboardFormat.Html, true);
        var service = new Mock<ICustomActionTransformService>(MockBehavior.Strict);
        service.Setup(action => action.TransformAsync(
            "prompt",
            "HTML-only text",
            null,
            It.IsAny<CancellationToken>(),
            It.IsAny<IProgress<double>>(),
            null,
            string.Empty)).ReturnsAsync(new CustomActionTransformResult("transformed text", AIServiceUsage.None));
        var executor = new PasteFormatExecutor(Mock.Of<IKernelService>(), service.Object, Mock.Of<IUserSettings>());

        var output = await executor.ExecutePasteFormatAsync(format, package.GetView(), PasteActionSource.CommandLine, CancellationToken.None, null);

        Assert.IsTrue(format.IsEnabled);
        Assert.AreEqual("transformed text", await output.GetView().GetTextAsync());
        service.VerifyAll();
    }
}
