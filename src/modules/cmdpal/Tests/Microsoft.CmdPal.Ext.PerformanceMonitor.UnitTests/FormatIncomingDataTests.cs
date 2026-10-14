// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class FormatIncomingDataTests
{
    [DataTestMethod]
    [DataRow(0f, "0.0 Kbps")]
    [DataRow(6400f, "50.0 Kbps")]
    [DataRow(12793f, "99.9 Kbps")]
    [DataRow(12800f, "100 Kbps")]
    [DataRow(127872f, "999 Kbps")]
    [DataRow(131072f, "1.0 Mbps")]
    [DataRow(6553600f, "50.0 Mbps")]
    [DataRow(13107200f, "100 Mbps")]
    [DataRow(134217728f, "1.0 Gbps")]
    [DataRow(6710886400f, "50.0 Gbps")]
    [DataRow(13421772800f, "100 Gbps")]
    public void AsBitsPerSecString_FormatsBoundaryValues(float bytesPerSecond, string expected)
    {
        Assert.AreEqual(expected, FormatIncomingData.AsBitsPerSecString(bytesPerSecond));
    }

    [DataTestMethod]
    [DataRow(0f, "0.0 KB/s")]
    [DataRow(50000f, "50.0 KB/s")]
    [DataRow(100000f, "100 KB/s")]
    [DataRow(999000f, "999 KB/s")]
    [DataRow(1000000f, "1.0 MB/s")]
    [DataRow(50000000f, "50.0 MB/s")]
    [DataRow(100000000f, "100 MB/s")]
    [DataRow(1000000000f, "1.0 GB/s")]
    [DataRow(50000000000f, "50.0 GB/s")]
    [DataRow(100000000000f, "100 GB/s")]
    public void AsBytesPerSecString_FormatsBoundaryValues(float bytesPerSecond, string expected)
    {
        Assert.AreEqual(expected, FormatIncomingData.AsBytesPerSecString(bytesPerSecond));
    }

    [TestMethod]
    public void AsBytesPerSecString_UsesDecimalScalingDistinctFromBinary()
    {
        const float bytesPerSecond = 51200f;

        Assert.AreEqual("51.2 KB/s", FormatIncomingData.AsBytesPerSecString(bytesPerSecond));
        Assert.AreEqual("50.0 KiB/s", FormatIncomingData.AsBinaryBytesPerSecString(bytesPerSecond));
    }

    [DataTestMethod]
    [DataRow(0f, "0.0 KiB/s")]
    [DataRow(51200f, "50.0 KiB/s")]
    [DataRow(102400f, "100 KiB/s")]
    [DataRow(1022976f, "999 KiB/s")]
    [DataRow(1048576f, "1.0 MiB/s")]
    [DataRow(52428800f, "50.0 MiB/s")]
    [DataRow(104857600f, "100 MiB/s")]
    [DataRow(1073741824f, "1.0 GiB/s")]
    [DataRow(53687091200f, "50.0 GiB/s")]
    [DataRow(107374182400f, "100 GiB/s")]
    public void AsBinaryBytesPerSecString_FormatsBoundaryValues(float bytesPerSecond, string expected)
    {
        Assert.AreEqual(expected, FormatIncomingData.AsBinaryBytesPerSecString(bytesPerSecond));
    }
}
