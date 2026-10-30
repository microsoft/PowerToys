// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Packaged;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ContrastMode = Microsoft.CmdPal.Common.IconContrastMode;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class PackagedAppIconProtocolProcessorTests
{
    [TestMethod]
    public void CacheContextUsesSurfaceThemeAndContrastWithoutPalette()
    {
        var value = PackagedAppIcons.ProtocolPrefix;
        var processor = IconProtocolRegistry.Find(value);
        Assert.AreSame(PackagedAppIconProtocolProcessor.Instance, processor);
        Assert.AreEqual(new IconRenderContext(ElementTheme.Light, default), processor!.GetCacheContext(value, new(ElementTheme.Light, default)));
        Assert.AreEqual(new IconRenderContext(ElementTheme.Dark, default), processor.GetCacheContext(value, new(ElementTheme.Dark, default)));
        Assert.AreEqual(new IconRenderContext(ElementTheme.Dark, new(ContrastMode.White)), processor.GetCacheContext(
            value, new(ElementTheme.Dark, new(ContrastMode.White, 0xFF001122, 0xFFFFFFFF))));
        Assert.IsFalse(processor.TryPrepareSynchronously(value, 20, new IconRenderContext(ElementTheme.Light, default), out _));
    }

    [TestMethod]
    [DataRow(ElementTheme.Light, ContrastMode.Standard, "theme-light")]
    [DataRow(ElementTheme.Dark, ContrastMode.Standard, "theme-dark")]
    [DataRow(ElementTheme.Light, ContrastMode.Black, "contrast-black")]
    [DataRow(ElementTheme.Dark, ContrastMode.White, "contrast-white")]
    [DataRow(ElementTheme.Dark, ContrastMode.High, "contrast-high")]
    public async Task PrepareAsync_MapsSurfaceContextToApps(ElementTheme theme, ContrastMode mode, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-icon-adapter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            foreach (var qualifier in new[] { "theme-light", "theme-dark", "contrast-black", "contrast-white", "contrast-high" })
            {
                File.WriteAllBytes(Path.Combine(root, $"Logo.targetsize-32_{qualifier}.png"), [0]);
            }

            // Only Apps produces requests in production. This adapter fixture bypasses PRI.
            var reference = PackagedAppIcons.ProtocolPrefix + "|" + Uri.EscapeDataString(root) + "|Logo.png|";
            using var result = await PackagedAppIconProtocolProcessor.Instance.PrepareAsync(reference, 20, new IconRenderContext(theme, new(mode)));
            Assert.AreEqual(Path.Combine(root, $"Logo.targetsize-32_{expected}.png"), result.FallbackIconStrings![0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task PrepareAsync_LeavesFallbackHandlingToTheHostWhenResolutionFails()
    {
        using var result = await PackagedAppIconProtocolProcessor.Instance.PrepareAsync(PackagedAppIcons.ProtocolPrefix, 20, new IconRenderContext(ElementTheme.Dark, default));
        Assert.AreEqual(IconProtocolProcessingResult.ResultKind.Empty, result.Kind);
    }
}
