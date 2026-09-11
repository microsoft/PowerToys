// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class GpuAdapterNamesTests
{
    [TestMethod]
    [DataRow("NVIDIA RTX A3000 Laptop GPU", "RTX A3000")]
    [DataRow("NVidia RTX GeForce A3000 Laptop GPU", "RTX A3000")]
    [DataRow("NVIDIA GeForce RTX 4070 Ti SUPER", "RTX 4070 Ti SUPER")]
    [DataRow("NVIDIA Quadro RTX 4000", "RTX 4000")]
    [DataRow("NVIDIA Quadro P4000", "Quadro P4000")]
    [DataRow("NVIDIA RTX 5000 Ada Generation", "RTX 5000 Ada")]
    [DataRow("AMD Radeon(TM) RX 7900 XTX", "RX 7900 XTX")]
    [DataRow("AMD Radeon Pro W6600", "Pro W6600")]
    [DataRow("AMD Radeon(TM) 780M Graphics", "Radeon 780M")]
    [DataRow("AMD Radeon(TM) Graphics", "Radeon Graphics")]
    [DataRow("ATI Radeon HD 5770", "Radeon HD 5770")]
    [DataRow("Intel(R) UHD Graphics 770", "UHD 770")]
    [DataRow("Intel(R) UHD Graphics", "UHD Graphics")]
    [DataRow("Intel(R) HD Graphics 630", "HD 630")]
    [DataRow("Intel(R) Iris(R) Xe Graphics", "Iris Xe")]
    [DataRow("Intel\u00ae Iris\u00ae Xe Graphics", "Iris Xe")]
    [DataRow("Intel(R) Arc(TM) A770 Graphics", "Arc A770")]
    [DataRow("Intel Arc\u2122 A770 Graphics", "Arc A770")]
    [DataRow("  NVIDIA   GeForce RTX 3050 Ti Laptop GPU  ", "RTX 3050 Ti")]
    public void GetShortName_RemovesKnownBrandingAndKeepsModelVariants(string description, string expected)
    {
        Assert.AreEqual(expected, GpuAdapterNames.GetShortName(description));
    }

    [TestMethod]
    [DataRow("Microsoft Basic Render Driver")]
    [DataRow("Contoso GeForce Graphics Device")]
    [DataRow("Qualcomm Adreno X1-85 GPU")]
    [DataRow("NVIDIA")]
    [DataRow("Intel Graphics")]
    [DataRow("GPU 0")]
    public void GetShortName_PreservesUnrecognizedOrGenericNames(string description)
    {
        Assert.AreEqual(description, GpuAdapterNames.GetShortName(description));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GetShortName_MissingDescriptionReturnsEmpty(string description)
    {
        Assert.AreEqual(string.Empty, GpuAdapterNames.GetShortName(description));
    }
}
