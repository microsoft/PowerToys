// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Models;

namespace WorkspacesCsharpLibrary.UnitTests;

[TestClass]
public class ApplicationIconTests
{
    [TestMethod]
    [DoNotParallelize]
    public void MissingExecutableUsesFallbackFromApplicationDirectory()
    {
        string originalDirectory = Environment.CurrentDirectory;
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            Environment.CurrentDirectory = directory.FullName;
            using var application = new BaseApplication { AppPath = Path.Combine(directory.FullName, "missing.exe") };

            Assert.IsNotNull(application.Icon);
            Assert.IsTrue(application.IsNotFound);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void CachedPwaLogoCanBeDrawnAndDisposedEvenWhenTheApplicationIsMissing()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            string path = Path.Combine(directory.FullName, "logo.png");
            using (var image = new Bitmap(16, 16))
            {
                image.Save(path, ImageFormat.Png);
            }

            using var application = new BaseApplication
            {
                AppPath = Path.Combine(directory.FullName, "chrome.exe"),
                PwaAppId = "cachedPwa",
            };
            IntPtr handle;
            using (var icon = application.LoadIcon(path))
            {
                Assert.IsTrue(application.IsNotFound);
                handle = icon.Handle;
                File.Delete(path);
                using var rendered = icon.ToBitmap();
                Assert.AreEqual(16, rendered.Width);
            }

            // A borrowed Icon leaves its HICON valid even after Dispose.
            IntPtr copy = CopyIcon(handle);
            try
            {
                Assert.AreEqual(IntPtr.Zero, copy);
            }
            finally
            {
                if (copy != IntPtr.Zero)
                {
                    _ = DestroyIcon(copy);
                }
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow("chrome")]
    [DataRow("msedge")]
    public void PwaAvailabilityFollowsTheLauncherProxyEvenForLegacySnapshots(string browserName)
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            string browser = Path.Combine(directory.FullName, browserName + ".exe");
            string proxy = Path.Combine(directory.FullName, browserName + "_proxy.exe");
            File.WriteAllBytes(browser, []);

            Assert.IsFalse(PwaHelper.HasLaunchTarget(browser, string.Empty, string.Empty));
            Assert.IsFalse(PwaHelper.HasLaunchTarget(browser, "PowerToys.Workspaces.Missing." + Guid.NewGuid().ToString("N"), "1"));

            File.WriteAllBytes(proxy, []);
            Assert.IsTrue(PwaHelper.HasLaunchTarget(browser, string.Empty, string.Empty));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr CopyIcon(IntPtr icon);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
