// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public partial class ShortcutThumbnailTests
{
    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiShellIconSize = 0x00000004;
    private const uint ShgfiPidl = 0x00000008;

    [TestMethod]
    [DataRow("namespace")]
    [DataRow("executable")]
    [DataRow("missingExecutable")]
    public async Task SmallAppShortcutIconUsesBaseIconWithoutChangingShortcut(string targetKind)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-shortcut-icon-{Guid.NewGuid():N}.lnk");
        try
        {
            CreateShortcut(shortcutPath, targetKind);
            var originalShortcut = File.ReadAllBytes(shortcutPath);
            var request = new ShellItemIconRequest(shortcutPath, jumbo: false);
            Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(request, out var locatedIcon));
            Assert.AreEqual(ShellIconIdentityKind.SystemImageList, locatedIcon.Identity.Kind);

            using var expected = ShellSystemImageListIconExtractor.Extract(
                locatedIcon.Identity.SystemImageListIndex,
                jumbo: false,
                requestedPixelSize: 32);
            Assert.IsNotNull(expected.SoftwareBitmap);

            using var result = await AppIconProtocolProcessor.Instance.PrepareAsync(
                AppIconProtocol.Create(shortcutPath),
                32,
                ElementTheme.Default);
            Assert.AreEqual(IconProtocolProcessingResult.ResultKind.PreparedIcon, result.Kind);
            using var actual = result.TakePreparedIcon();
            Assert.IsNotNull(actual);
            Assert.IsNotNull(actual.SoftwareBitmap);
            Assert.AreEqual(expected.SoftwareBitmap.PixelWidth, actual.SoftwareBitmap.PixelWidth);
            Assert.AreEqual(expected.SoftwareBitmap.PixelHeight, actual.SoftwareBitmap.PixelHeight);
            CollectionAssert.AreEqual(GetPixels(expected.SoftwareBitmap), GetPixels(actual.SoftwareBitmap), "App shortcut icons must contain the base icon rather than the shortcut overlay.");
            CollectionAssert.AreEqual(originalShortcut, File.ReadAllBytes(shortcutPath), "Loading an icon must leave the shortcut unchanged.");
        }
        finally
        {
            File.Delete(shortcutPath);
        }
    }

    [TestMethod]
    [DataRow("namespace")]
    [DataRow("executable")]
    [DataRow("missingExecutable")]
    public async Task PublicSmallShortcutThumbnailPreservesDecoratedShellIcon(string targetKind)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-shortcut-thumbnail-{Guid.NewGuid():N}.lnk");
        try
        {
            CreateShortcut(shortcutPath, targetKind);
            var originalShortcut = File.ReadAllBytes(shortcutPath);
            using var expected = await GetDecoratedShortcutBitmapAsync(shortcutPath);
            using var thumbnail = await ThumbnailHelper.GetThumbnail(shortcutPath, jumbo: false);
            Assert.IsNotNull(thumbnail);
            var decoder = await BitmapDecoder.CreateAsync(thumbnail);
            using var actual = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            Assert.AreEqual(expected.PixelWidth, actual.PixelWidth);
            Assert.AreEqual(expected.PixelHeight, actual.PixelHeight);
            CollectionAssert.AreEqual(GetPixels(expected), GetPixels(actual), "Public shortcut thumbnails must preserve the decorated Shell icon.");
            CollectionAssert.AreEqual(originalShortcut, File.ReadAllBytes(shortcutPath), "Loading a thumbnail must leave the shortcut unchanged.");
        }
        finally
        {
            File.Delete(shortcutPath);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetKind)
    {
        var targetPath = targetKind switch
        {
            "namespace" => "shell:AppsFolder",
            "executable" => Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            _ => Path.ChangeExtension(shortcutPath, ".exe"),
        };
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            if (targetKind == "namespace")
            {
                CreateNamespaceShortcut(shortcutPath, targetPath);
            }
            else
            {
                shortcut.TargetPath = targetPath;
                shortcut.Save();
            }

            dynamic savedShortcut = shell.CreateShortcut(shortcutPath);
            try
            {
                if (targetKind == "namespace")
                {
                    Assert.IsTrue(string.IsNullOrEmpty((string)savedShortcut.TargetPath), "The namespace fixture must have no ordinary file target.");
                }
                else
                {
                    Assert.IsTrue(string.Equals(targetPath, (string)savedShortcut.TargetPath, StringComparison.OrdinalIgnoreCase));
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(savedShortcut);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static async Task<SoftwareBitmap> GetDecoratedShortcutBitmapAsync(string path)
    {
        nint itemIdList = 0;
        var fileInfo = default(ShellFileInfo);
        try
        {
            Marshal.ThrowExceptionForHR(NativeMethods.SHParseDisplayName(path, 0, out itemIdList, 0, out _));
            Assert.AreNotEqual(nint.Zero, itemIdList);
            var result = NativeMethods.SHGetFileInfo(
                itemIdList,
                0,
                ref fileInfo,
                (uint)Marshal.SizeOf<ShellFileInfo>(),
                ShgfiIcon | ShgfiShellIconSize | ShgfiPidl);
            Assert.AreNotEqual(nint.Zero, result);
            Assert.AreNotEqual(nint.Zero, fileInfo.IconHandle);

            using var icon = System.Drawing.Icon.FromHandle(fileInfo.IconHandle);
            using var bitmap = icon.ToBitmap();
            using var png = new MemoryStream();
            bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(png.ToArray());
                await writer.StoreAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
        finally
        {
            if (fileInfo.IconHandle != 0)
            {
                _ = NativeMethods.DestroyIcon(fileInfo.IconHandle);
            }

            if (itemIdList != 0)
            {
                Marshal.FreeCoTaskMem(itemIdList);
            }
        }
    }

    private static void CreateNamespaceShortcut(string path, string target)
    {
        nint itemIdList = 0;
        object? shortcut = null;
        try
        {
            Marshal.ThrowExceptionForHR(NativeMethods.SHParseDisplayName(target, 0, out itemIdList, 0, out _));
            Assert.AreNotEqual(nint.Zero, itemIdList);
            shortcut = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-c000-000000000046"))!);
            Assert.IsNotNull(shortcut);
            var link = (INamespaceShellLink)shortcut;
            Marshal.ThrowExceptionForHR(link.SetIDList(itemIdList));
            ((System.Runtime.InteropServices.ComTypes.IPersistFile)shortcut).Save(path, true);
        }
        finally
        {
            if (shortcut is not null)
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (itemIdList != 0)
            {
                Marshal.FreeCoTaskMem(itemIdList);
            }
        }
    }

    private static byte[] GetPixels(SoftwareBitmap bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyToBuffer(pixels.AsBuffer());
        return pixels;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct ShellFileInfo
    {
        public nint IconHandle;
        public int IconIndex;
        public uint Attributes;
        public fixed char DisplayName[260];
        public fixed char TypeName[80];
    }

    [ComImport]
    [Guid("000214f9-0000-0000-c000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INamespaceShellLink
    {
        [PreserveSig]
        int GetPath(nint file, int characterCount, nint findData, uint flags);

        [PreserveSig]
        int GetIDList(out nint itemIdList);

        [PreserveSig]
        int SetIDList(nint itemIdList);
    }

    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial nint SHGetFileInfo(
            nint itemIdList,
            uint fileAttributes,
            ref ShellFileInfo fileInfo,
            uint fileInfoSize,
            uint flags);

        [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int SHParseDisplayName(
            string displayName,
            nint bindContext,
            out nint itemIdList,
            uint attributes,
            out uint parsedAttributes);

        [LibraryImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int DestroyIcon(nint icon);
    }
}
