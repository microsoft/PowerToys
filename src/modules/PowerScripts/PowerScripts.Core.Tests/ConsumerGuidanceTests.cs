// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class ConsumerGuidanceTests
{
    [TestMethod]
    public void ThemeActionsAcceptOnlyNoInputNoOutputAndCanShareHotkeyBindings()
    {
        Assert.IsTrue(PowerScriptConsumerGuidance.Allows([PowerScriptConsumer.LightSwitch, PowerScriptConsumer.KeyboardManager], "none", "none"));
        Assert.IsFalse(PowerScriptConsumerGuidance.Supports(PowerScriptConsumer.LightSwitch, "text", "none"));
        Assert.IsFalse(PowerScriptConsumerGuidance.Supports(PowerScriptConsumer.LightSwitch, "none", "text"));
        Assert.IsFalse(PowerScriptConsumerGuidance.Supports(PowerScriptConsumer.LightSwitch, "none", "files"));
    }

    [TestMethod]
    public void OptionalGuidanceDoesNotRestrictCustomContracts()
    {
        Assert.IsTrue(PowerScriptConsumerGuidance.Allows([], "audio", "none"));
        Assert.IsFalse(PowerScriptConsumerGuidance.Allows([], "video", "text"));
    }

    [TestMethod]
    public void HotkeyAndPaletteIntersectWithoutAllowingUnsupportedOutput()
    {
        string[] selected = [PowerScriptConsumer.KeyboardManager, PowerScriptConsumer.CommandPalette];
        Assert.IsTrue(PowerScriptConsumerGuidance.CanCombine(selected));
        Assert.IsTrue(PowerScriptConsumerGuidance.Allows(selected, "none", "text"));
        Assert.IsFalse(PowerScriptConsumerGuidance.Allows(selected, "none", "files"));
        Assert.IsFalse(PowerScriptConsumerGuidance.CanCombine(selected.Append("file-explorer")));
        Assert.IsFalse(PowerScriptConsumerGuidance.CanCombine(selected.Append(PowerScriptConsumer.AdvancedPaste)));
    }

    [TestMethod]
    public void ClipboardAndExplorerShareFileAndInlineMediaContracts()
    {
        string[] selected = [PowerScriptConsumer.AdvancedPaste, "file-explorer"];
        foreach (var input in new[] { "files", "image", "audio" })
        {
            Assert.IsTrue(PowerScriptConsumerGuidance.Allows(selected, input, "text"));
        }

        Assert.IsFalse(PowerScriptConsumerGuidance.Allows(selected, "text", "text"));
        Assert.IsFalse(PowerScriptConsumerGuidance.Allows(selected, "files", "none"));
        Assert.IsFalse(PowerScriptConsumerGuidance.CanCombine(["unknown-consumer"]));
    }
}
