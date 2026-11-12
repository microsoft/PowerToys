// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.PreviewPane.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class ThumbnailProviderLogTests
{
    [TestMethod]
    [DataRow("Failed to copy thumbnail input. Error: -2147024891")]
    [DataRow("Thumbnail provider PowerToys.SvgThumbnailProvider.exe did not complete. Error: 1460")]
    [DataRow("Thumbnail provider PowerToys.SvgThumbnailProvider.exe exited with code 1.")]
    [DataRow("Thumbnail bitmap was not generated or could not be loaded.")]
    [DataRow("Thumbnail provider PowerToys.SvgThumbnailProvider.exe failed. Error: access denied")]
    public void ReportsGenerationFailuresAfterInitialization(string diagnostic)
    {
        var logText = $"[info] SvgThumbnailProvider logger is initialized{Environment.NewLine}{diagnostic}";

        Assert.IsTrue(ThumbnailProviderLog.HasGenerationFailure(logText, "SvgThumbnailProvider"));
    }

    [TestMethod]
    [DataRow("[info] SvgThumbnailProvider logger is initialized")]
    [DataRow("[error] Failed to remove thumbnail temporary file. Error: 32")]
    [DataRow("[error] Failed to remove thumbnail browser data. Error: 32")]
    public void DoesNotTreatInitializationOrCleanupAsGenerationFailure(string logText)
    {
        Assert.IsFalse(ThumbnailProviderLog.HasGenerationFailure(logText, "SvgThumbnailProvider"));
    }
}
