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
            const string AppId = "TestBrowser.Pwa";
            bool ResolveItem(string path) => path == @"shell:AppsFolder\" + AppId;
            Assert.IsFalse(PwaHelper.HasLaunchTarget(browser, "Missing", "1", ResolveItem));
            Assert.IsTrue(PwaHelper.HasLaunchTarget(browser, AppId, "1", ResolveItem));
            Assert.IsFalse(PwaHelper.HasLaunchTarget(browser, AppId, string.Empty, ResolveItem));

            File.WriteAllBytes(proxy, []);
            Assert.IsTrue(PwaHelper.HasLaunchTarget(browser, string.Empty, string.Empty));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(".png")]
    [DataRow(".ico")]
    public void CorruptPwaImageFallsBackToTheExecutableIcon(string extension)
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            string executable = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            string corrupt = Path.Combine(directory.FullName, "bad" + extension);
            File.WriteAllBytes(corrupt, [0xFF, 0xFE, 0xFD]);
            using var application = new BaseApplication { AppPath = executable, PwaAppId = "pwa" };
            using var expected = Icon.ExtractAssociatedIcon(executable);
            using var actual = application.LoadIcon(corrupt);

            Assert.IsFalse(application.IsNotFound);
            CollectionAssert.AreEqual(IconPixels(expected!), IconPixels(actual));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void BitmapConversionReleasesTheOriginalHiconWhileItsCopyIsStillUsable()
    {
        using var bitmap = new Bitmap(16, 16);
        IntPtr original = bitmap.GetHicon();
        using var icon = BaseApplication.CopyAndReleaseIconHandle(original);
        IntPtr probe = CopyIcon(original);
        try
        {
            Assert.AreEqual(IntPtr.Zero, probe, "The original bitmap HICON is still allocated.");
            using var rendered = icon.ToBitmap();
            Assert.AreEqual(16, rendered.Width);
        }
        finally
        {
            if (probe != IntPtr.Zero)
            {
                _ = DestroyIcon(probe);
                _ = DestroyIcon(original);
            }
        }
    }

    [TestMethod]
    [DataRow("Test.Workspaces_1.2.3.4_neutral__8wekyb3d8bbwe", null, @"E:\Relocated\app.exe")]
    [DataRow(null, "Test.Workspaces_8wekyb3d8bbwe!CustomEntry", @"D:\Moved\app.exe")]
    [DataRow(null, null, @"D:\wiNDowsApPs\Test.Workspaces_1.2.3.4_x86__8wekyb3d8bbwe\app.exe")]
    [DataRow(null, null, @"E:\Other\WindowsApps\Test.Workspaces_1.2.3.4_arm64__8wekyb3d8bbwe\bin\app.exe")]
    public void InstalledPackageIconUsesCapturedIdentityOrLegacyPackageDirectory(string? fullName, string? appId, string path)
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            string logo = Path.Combine(directory.FullName, "package.png");
            using (var image = new Bitmap(16, 16))
            {
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(Color.OrangeRed);
                }

                image.Save(logo, ImageFormat.Png);
            }

            using var application = new BaseApplication { PackageFullName = fullName, AppUserModelId = appId, AppPath = path };
            int lookups = 0;
            using var icon = application.LoadIcon(null, family =>
            {
                Assert.AreEqual("Test.Workspaces_8wekyb3d8bbwe", family);
                lookups++;
                return (true, new Uri(logo));
            });
            using var rendered = icon.ToBitmap();

            Assert.AreEqual(1, lookups);
            Assert.AreEqual(string.IsNullOrEmpty(fullName), application.IsNotFound);
            Assert.AreEqual(Color.OrangeRed.ToArgb(), rendered.GetPixel(8, 8).ToArgb());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PackageAvailabilityIsIndependentOfWhetherALogoIsPresent(bool installed)
    {
        using var application = new BaseApplication
        {
            PackageFullName = "Test.Workspaces_1.2.3.4_neutral__8wekyb3d8bbwe",
            AppUserModelId = "Test.Workspaces_8wekyb3d8bbwe!App",
        };
        using var icon = application.LoadIcon(null, family => (installed, null));
        Assert.AreEqual(!installed, application.IsNotFound);
        Assert.IsNotNull(icon);
    }

    [TestMethod]
    public void LegacyPackageWithItsOriginalExecutablePresentStaysLaunchable()
    {
        using var application = new BaseApplication
        {
            AppUserModelId = "Test.Workspaces_8wekyb3d8bbwe!App",
            AppPath = Environment.ProcessPath,
        };
        using var icon = application.LoadIcon(null, _ => (true, null));
        Assert.IsFalse(application.IsNotFound);
        Assert.IsNotNull(icon);
    }

    [TestMethod]
    public void PwaIconLookupSeparatesBrowsersAndFindsTheCapturedProfile()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            const string AppId = "samplepwa";
            var resolver = new PwaHelper(directory.FullName);
            string WriteIcon(string relative)
            {
                string path = Path.Combine(directory.FullName, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, []);
                return path;
            }

            const string Browser = @"C:\Browser\chrome.exe";
            const string DefaultId = "Chrome._crx_" + AppId;
            const string ProfileId = DefaultId + ".UserData.Profile1";
            string edge = WriteIcon(@"Microsoft\Edge\User Data\Default\Web Applications\_crx__samplepwa\Edge.ico");
            Assert.AreEqual(string.Empty, resolver.FindIcon(AppId, Browser, DefaultId));
            string chrome = WriteIcon(@"Google\Chrome\User Data\Profile 1\Web Applications\Manifest Resources\samplepwa\Icons\Chrome.png");
            Assert.AreEqual(chrome, resolver.FindIcon(AppId, Browser, null));
            string chromeDefault = WriteIcon(@"Google\Chrome\User Data\Default\Web Applications\_crx_samplepwa\Chrome.ico");
            Assert.AreEqual(edge, resolver.FindIcon(AppId, @"C:\Browser\MSEdge.exe", null));
            Assert.AreEqual(chromeDefault, resolver.FindIcon(AppId, Browser, DefaultId));
            Assert.AreEqual(chrome, resolver.FindIcon(AppId, Browser, ProfileId));
            Assert.AreEqual(chrome, resolver.FindIcon(AppId, null, ProfileId));
            Assert.AreEqual(string.Empty, resolver.FindIcon(AppId, Browser, "Chrome._crx_otherapp"));
            Assert.AreEqual(string.Empty, resolver.FindIcon(AppId, Browser, null));
            Assert.AreEqual(string.Empty, resolver.FindIcon(AppId, null, null));

            File.Delete(chrome);
            Assert.AreEqual(string.Empty, resolver.FindIcon(AppId, Browser, ProfileId));
            string replacement = WriteIcon(@"Google\Chrome\User Data\Profile 1\Web Applications\_crx_samplepwa\Updated.ico");
            Assert.AreEqual(replacement, resolver.FindIcon(AppId, Browser, ProfileId));
            Assert.AreEqual(string.Empty, resolver.FindIcon(@"..\outside", Browser, DefaultId));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void ChromiumShortenedAumidsMatchBothEndsOfTheAppAndProfileComponents()
    {
        const string AppId = "abcdefghijklmnopabcdefghijklmnop";
        const string Browser = @"C:\Browser\chrome.exe";
        const string DefaultAumid = "Chrome._crx_abcdefghijabcdefghijklmnop";
        const string ProfileAumid = "Chrome._crx_abcdeghijklmnop.UserData.Profile1";
        const string LongProfileAumid = "Chrome._crx_abcdeghijklmnop.UserData.L1234567890";
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            string Install(string profile)
            {
                var icons = directory.CreateSubdirectory(Path.Combine(@"Google\Chrome\User Data", profile, "Web Applications", "_crx_" + AppId));
                string icon = Path.Combine(icons.FullName, "App.ico");
                File.WriteAllBytes(icon, []);
                return icon;
            }

            var resolver = new PwaHelper(directory.FullName);
            string normal = Install("Default");
            string first = Install("Profile 1");
            string longProfile = Install("LongProfileNameWithSharedTail1234567890");
            Assert.AreEqual(normal, resolver.FindIcon(AppId, Browser, DefaultAumid));
            Assert.AreEqual(first, resolver.FindIcon(AppId, Browser, ProfileAumid));
            Assert.AreEqual(first, resolver.FindIcon(AppId, Browser, "Chrome._crx_" + AppId + ".UserData.Profile1"));
            Assert.AreEqual(longProfile, resolver.FindIcon(AppId, Browser, LongProfileAumid));
            Assert.AreEqual(string.Empty, resolver.FindIcon(AppId, Browser, DefaultAumid[..^1] + "a"));

            Install("LongProfileNameZZZZSharedTail1234567890");
            Assert.AreEqual(string.Empty, resolver.FindIcon(AppId, Browser, LongProfileAumid), "The shortened profile component now matches two installations.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static byte[] IconPixels(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr CopyIcon(IntPtr icon);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
