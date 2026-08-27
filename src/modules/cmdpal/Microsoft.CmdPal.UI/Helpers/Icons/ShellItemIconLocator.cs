// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class ShellItemIconLocator : IShellItemIconLocator
{
    private const uint ShgfiSystemIconIndex = 0x000004000;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;

    public static ShellItemIconLocator Instance { get; } = new();

    private ShellItemIconLocator()
    {
    }

    public unsafe bool TryLocate(
        ShellItemIconRequest request,
        out LocatedShellIcon locatedIcon)
    {
        if (request.LocationMode == ShellItemIconLocationMode.FileType)
        {
            using var typeErrorMode = ShellThreadErrorModeScope.SuppressShellDialogs();
            if (TryGetSystemImageListIndex(
                    request.ItemPath,
                    FileAttributeNormal,
                    ShgfiSystemIconIndex | ShgfiUseFileAttributes,
                    out var typeIconIndex))
            {
                locatedIcon = new LocatedShellIcon(
                    request,
                    ShellIconIdentity.FromSystemImageList(typeIconIndex, request.Jumbo));
                return true;
            }

            locatedIcon = default;
            return false;
        }

        if (ShellItemIconRequestClassifier.IsDirectImagePath(request.ItemPath))
        {
            locatedIcon = new LocatedShellIcon(
                request,
                ShellIconIdentity.FromItemThumbnail(request.ItemPath, request.Jumbo));
            return true;
        }

        using var errorMode = ShellThreadErrorModeScope.SuppressShellDialogs();

        if (TryGetSystemImageListIndex(
                request.ItemPath,
                fileAttributes: 0,
                ShgfiSystemIconIndex,
                out var iconIndex))
        {
            locatedIcon = new LocatedShellIcon(
                request,
                ShellIconIdentity.FromSystemImageList(iconIndex, request.Jumbo));
            return true;
        }

        if (TryGetSystemImageListIndex(
                request.ItemPath,
                FileAttributeNormal,
                ShgfiSystemIconIndex | ShgfiUseFileAttributes,
                out iconIndex))
        {
            // The registered type icon is safe to share by image-list identity. Do not
            // retain the raw-path alias: the missing item can later appear with a custom icon.
            locatedIcon = new LocatedShellIcon(
                request,
                ShellIconIdentity.FromSystemImageList(iconIndex, request.Jumbo),
                CacheRawRequestAlias: false);
            return true;
        }

        locatedIcon = new LocatedShellIcon(
            request,
            ShellIconIdentity.FromItemPath(request.ItemPath, request.Jumbo));
        return true;
    }

    private static unsafe bool TryGetSystemImageListIndex(
        string itemPath,
        uint fileAttributes,
        uint flags,
        out int iconIndex)
    {
        var fileInfo = default(ShellFileInfo);
        var result = NativeMethods.SHGetFileInfo(
            itemPath,
            fileAttributes,
            ref fileInfo,
            (uint)sizeof(ShellFileInfo),
            flags);
        iconIndex = fileInfo.IconIndex;
        return result != 0 && iconIndex >= 0;
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

    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial nint SHGetFileInfo(
            string path,
            uint fileAttributes,
            ref ShellFileInfo fileInfo,
            uint fileInfoSize,
            uint flags);
    }
}
