// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
[SupportedOSPlatform("windows")]
public class TrayIconAssetsTests
{
    [DataRow("disabled.ico")]
    [DataRow("expirable.ico")]
    [DataRow("indefinite.ico")]
    [DataRow("timed.ico")]
    [DataTestMethod]
    public void TrayIcon_CompressedFrames_LoadAtOriginalResolutions(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Awake", fileName);
        int[] resolutions = [16, 24, 32, 48, 128];
        foreach (int resolution in resolutions)
        {
            using Icon icon = new(path, resolution, resolution);
            Assert.AreEqual(resolution, icon.Width);
            Assert.AreEqual(resolution, icon.Height);
            Assert.AreNotEqual(IntPtr.Zero, icon.Handle);
        }
    }
}
