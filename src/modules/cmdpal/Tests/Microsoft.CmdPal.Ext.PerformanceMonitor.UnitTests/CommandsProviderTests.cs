// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class CommandsProviderTests
{
    [TestMethod]
    [DataRow("com.microsoft.cmdpal.cpu_widget")]
    [DataRow("com.microsoft.cmdpal.memory_widget")]
    [DataRow("com.microsoft.cmdpal.network_widget")]
    [DataRow("com.microsoft.cmdpal.disk_widget")]
    [DataRow("com.microsoft.cmdpal.gpu_widget")]
    public void GetCommandItem_ResolvesNestedWidgetPages(string commandId)
    {
        using var provider = new PerformanceMonitorCommandsProvider();

        var item = provider.GetCommandItem(commandId);

        Assert.IsNotNull(item, $"{commandId} should be resolvable so it can be pinned");
        Assert.AreEqual(commandId, item.Command.Id);
    }

    [TestMethod]
    public void GetCommandItem_ReturnsNullForUnknownIds()
    {
        using var provider = new PerformanceMonitorCommandsProvider();

        Assert.IsNull(provider.GetCommandItem("com.microsoft.cmdpal.not_a_widget"));
        Assert.IsNull(provider.GetCommandItem(string.Empty));
    }

    [TestMethod]
    public void GetCommandItem_ReturnsNullWhileSoftDisabled()
    {
        using var provider = new PerformanceMonitorCommandsProvider(softDisabled: true);

        Assert.IsNull(provider.GetCommandItem("com.microsoft.cmdpal.cpu_widget"));
    }
}
