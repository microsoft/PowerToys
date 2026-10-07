// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class Win32AppReaderTests
{
    [TestMethod]
    public void InternetShortcut_AllowsSharedWritesAndReleasesTheReadHandle()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CmdPal-sharing-{Guid.NewGuid():N}.url");
        try
        {
            File.WriteAllText(path, "[InternetShortcut]\nURL=steam://rungameid/123\nIconFile=C:\\Icons\\game.ico\nIgnored=remaining lines");
            using (var writer = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            {
                var program = Win32AppReader.LoadFromPath(path, asRunCommand: false);
                Assert.IsTrue(program.Valid);
                Assert.AreEqual("steam://rungameid/123", program.TargetPath);
            }

            // The early break after URL and IconFile must dispose the reader.
            using var exclusiveWriter = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void InternetShortcut_SharingViolationIsRetryable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CmdPal-sharing-{Guid.NewGuid():N}.url");
        try
        {
            File.WriteAllText(path, "[InternetShortcut]\nURL=steam://rungameid/123");
            using (var writer = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var program = Win32AppReader.LoadFromPath(path, asRunCommand: false);
                Assert.IsFalse(program.Valid);
                Assert.IsTrue(program.RetryableReadFailure);
            }

            Assert.IsTrue(Win32AppReader.LoadFromPath(path, asRunCommand: false).Valid);
            using var exclusiveWriter = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow("missing-file", false)]
    [DataRow("missing-directory", false)]
    [DataRow("invalid-name", false)]
    [DataRow("invalid-argument", false)]
    [DataRow("unsupported-path", false)]
    [DataRow("network-path", false)]
    [DataRow("network-disconnected", false)]
    [DataRow("network-name", false)]
    [DataRow("no-network", false)]
    [DataRow("network-unreachable", false)]
    [DataRow("host-unreachable", false)]
    [DataRow("access-denied", false)]
    [DataRow("io-access-denied", false)]
    [DataRow("com-access-denied", false)]
    [DataRow("sharing", true)]
    [DataRow("com-sharing", true)]
    public void Recovery_ReadFailureClassificationSeparatesPermanentErrors(string kind, bool retryable)
    {
        Exception exception = kind switch
        {
            "missing-file" => new FileNotFoundException(),
            "missing-directory" => new DirectoryNotFoundException(),
            "invalid-name" => new IOException("Invalid name", unchecked((int)0x8007007B)),
            "invalid-argument" => new ArgumentException("Invalid path"),
            "unsupported-path" => new NotSupportedException("Unsupported path"),
            "network-path" => new IOException("Network path unavailable", unchecked((int)0x80070035)),
            "network-disconnected" => new IOException("Network name unavailable", unchecked((int)0x80070040)),
            "network-name" => new IOException("Network name not found", unchecked((int)0x80070043)),
            "no-network" => new IOException("Network unavailable", unchecked((int)0x800704C6)),
            "network-unreachable" => new IOException("Network unreachable", unchecked((int)0x800704CF)),
            "host-unreachable" => new IOException("Host unreachable", unchecked((int)0x800704D0)),
            "access-denied" => new UnauthorizedAccessException(),
            "io-access-denied" => new IOException("Access denied", unchecked((int)0x80070005)),
            "com-access-denied" => Marshal.GetExceptionForHR(unchecked((int)0x80030005))!,
            "sharing" => new IOException("Sharing violation", unchecked((int)0x80070020)),
            "com-sharing" => Marshal.GetExceptionForHR(unchecked((int)0x80030020))!,
            _ => throw new ArgumentException("Unknown fixture", nameof(kind)),
        };
        Assert.AreEqual(retryable, PathHelpers.IsRetryableReadFailure(exception));
    }

    [TestMethod]
    [DataRow(null, 0)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", 0)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", 3)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", -4)]
    [DataRow("C:\\Icons, custom\\APP.ICO", 0)]
    public void Shortcut_UsesIconLocationWithoutChangingLaunchPath(string iconPath, int iconIndex)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-icon-{Guid.NewGuid():N}.lnk");
        var targetPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = "/c echo shortcut";
            shortcut.WorkingDirectory = Environment.SystemDirectory;
            shortcut.Description = "Shortcut description";
            if (iconPath is not null)
            {
                shortcut.IconLocation = FormattableString.Invariant($"{iconPath},{iconIndex}");
            }

            shortcut.Save();

            var program = Win32AppReader.LoadFromPath(shortcutPath, asRunCommand: false);
            Assert.IsNotNull(program);
            Assert.IsTrue(program.Valid);
            Assert.AreEqual("Shortcut description", program.Description);
            var expectedIcon = iconPath is null
                ? targetPath
                : FormattableString.Invariant($"{Environment.ExpandEnvironmentVariables(iconPath)},{iconIndex}");
            Assert.IsTrue(string.Equals(expectedIcon, program.IconLocation, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(string.Equals(targetPath, program.TargetPath, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(shortcutPath, program.LnkFilePath);
            Assert.AreEqual(Environment.SystemDirectory, program.WorkingDirectory);

            var payload = Win32AppPayload.From(program);
            Assert.AreEqual(Environment.SystemDirectory, payload.WorkingDirectory);
            var app = payload.ToAppItem();
            Assert.AreEqual(shortcutPath, app.LaunchTarget);
            Assert.AreEqual("/c echo shortcut", app.LaunchArguments);
            var item = new AppListItem(app);
            Assert.IsTrue(AppIconProtocol.TryParse(item.Icon.Light.Icon, out var candidates, out var jumbo));
            Assert.IsFalse(jumbo);
            CollectionAssert.AreEqual(
                iconPath is null ? new[] { targetPath } : new[] { expectedIcon, targetPath },
                candidates,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            File.Delete(shortcutPath);
        }
    }

    [TestMethod]
    public void Shortcut_MissingTargetRetainsItsPathWithoutSearchingNearbyFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"CmdPal-missing-target-{Guid.NewGuid():N}");
        var target = Path.Combine(root, "Missing", "App.exe");
        var shortcutPath = Path.Combine(root, "App.lnk");
        Directory.CreateDirectory(root);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            shortcut.TargetPath = target;
            shortcut.Save();
            Directory.CreateDirectory(Path.Combine(root, "Other"));
            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), Path.Combine(root, "Other", "App.exe"));
            var shortcutBytes = File.ReadAllBytes(shortcutPath);

            var program = Win32AppReader.LoadFromPath(shortcutPath, asRunCommand: false);
            Assert.IsFalse(program.Valid);
            Assert.IsTrue(string.Equals(target, program.TargetPath, StringComparison.OrdinalIgnoreCase));
            CollectionAssert.AreEqual(shortcutBytes, File.ReadAllBytes(shortcutPath));
            using var writer = File.Open(shortcutPath, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Shortcut_UnreadableIsInvalid(bool createInvalidFile)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-invalid-{Guid.NewGuid():N}.lnk");
        var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        try
        {
            if (createInvalidFile)
            {
                File.WriteAllText(shortcutPath, "Invalid shortcut");
            }

            Assert.IsNull(ShellLinkReader.Read(shortcutPath));
            Assert.IsFalse(Win32AppReader.LoadFromPath(shortcutPath, asRunCommand: false).Valid);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
            File.Delete(shortcutPath);
        }
    }
}
