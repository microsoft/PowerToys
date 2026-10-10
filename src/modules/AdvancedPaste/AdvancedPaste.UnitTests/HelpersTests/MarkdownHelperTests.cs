// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public class MarkdownHelperTests
{
    [TestMethod]
    public async Task ToMarkdownAsync_ExtractsHtmlFragmentFromClipboardEnvelope()
    {
        var dataPackage = new DataPackage();
        dataPackage.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat("<p>Hello <strong>world</strong></p>"));

        var result = await MarkdownHelper.ToMarkdownAsync(dataPackage.GetView(), CancellationToken.None);

        StringAssert.Contains(result, "Hello **world**");
        Assert.IsFalse(result.Contains("StartHTML", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("StartFragment", StringComparison.Ordinal));
    }
}
