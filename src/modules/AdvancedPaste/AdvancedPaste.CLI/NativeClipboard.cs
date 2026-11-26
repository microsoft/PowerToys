// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AdvancedPaste.Cli;

internal static partial class NativeClipboard
{
    private const uint BitmapFormat = 2;
    private const uint DibFormat = 8;
    private const uint UnicodeTextFormat = 13;
    private const uint FileDropFormat = 15;
    private const uint MoveableMemory = 0x0002;
    private const int BitmapFileHeaderSize = 14;
    private const int DropFilesHeaderSize = 20;
    private const int DropFilesWideFlagOffset = 16;
    private const int ClipboardRetryDelay = 100;

    private static readonly IntPtr MessageOnlyWindowParent = new(-3);

    internal static ClipboardSnapshot Read()
    {
        var htmlFormat = GetHtmlFormat();
        byte[]? text;
        byte[]? html;
        IReadOnlyList<string> paths;
        Bitmap? bitmap;

        OpenWithRetry(IntPtr.Zero, retries: 10);
        try
        {
            text = ReadMemoryBlock(UnicodeTextFormat);
            html = ReadMemoryBlock(htmlFormat);
            paths = ReadFileDrop();

            // Windows can synthesize CF_BITMAP from DIB formats. Copy the borrowed bitmap before closing the clipboard.
            var handle = GetClipboardData(BitmapFormat);
            bitmap = handle != IntPtr.Zero ? Image.FromHbitmap(handle) : null;
        }
        finally
        {
            CloseClipboard();
        }

        using (bitmap)
        {
            byte[]? bitmapBytes = null;
            if (bitmap is not null)
            {
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                bitmapBytes = stream.ToArray();
            }

            return new ClipboardSnapshot(DecodeUnicodeText(text), DecodeHtml(html), paths, bitmapBytes);
        }
    }

    internal static void Write(ClipboardSnapshot content)
    {
        var handles = new List<ClipboardHandle>(4);

        // The window becomes the clipboard owner when EmptyClipboard is called.
        var window = CreateWindowEx(0, "STATIC", null, 0, 0, 0, 0, 0, MessageOnlyWindowParent, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        try
        {
            // Prepare all data before replacing the clipboard so allocation or conversion errors leave it intact.
            if (content.Text is not null)
            {
                handles.Add(CreateMemoryBlock(UnicodeTextFormat, Encoding.Unicode.GetBytes(content.Text + '\0')));
            }

            if (content.Html is not null)
            {
                handles.Add(CreateMemoryBlock(GetHtmlFormat(), Encoding.UTF8.GetBytes(content.Html + '\0')));
            }

            if (content.FilePaths.Count > 0)
            {
                handles.Add(CreateMemoryBlock(FileDropFormat, EncodeFileDrop(content.FilePaths)));
            }

            if (content.BitmapBytes is not null)
            {
                handles.Add(CreateDibBlock(content.BitmapBytes));
            }

            OpenWithRetry(window, retries: 5);
            try
            {
                if (!EmptyClipboard())
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                }

                for (var index = 0; index < handles.Count; index++)
                {
                    var data = handles[index];
                    if (SetClipboardData(data.Format, data.Handle) == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastPInvokeError());
                    }

                    // Eagerly published data belongs to the system and survives the owner window and process.
                    handles[index] = data with { Handle = IntPtr.Zero };
                }
            }
            finally
            {
                CloseClipboard();
            }
        }
        finally
        {
            foreach (var data in handles)
            {
                if (data.Handle != IntPtr.Zero)
                {
                    GlobalFree(data.Handle);
                }
            }

            DestroyWindow(window);
        }
    }

    private static uint GetHtmlFormat()
    {
        var format = RegisterClipboardFormat("HTML Format");
        return format != 0 ? format : throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    private static void OpenWithRetry(IntPtr window, int retries)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (OpenClipboard(window))
            {
                return;
            }

            if (attempt == retries)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            Thread.Sleep(ClipboardRetryDelay);
        }
    }

    private static byte[]? ReadMemoryBlock(uint format)
    {
        var handle = GetClipboardData(format);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var size = checked((int)GlobalSize(handle));
        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        try
        {
            var bytes = new byte[size];
            Marshal.Copy(pointer, bytes, 0, size);
            return bytes;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static string? DecodeUnicodeText(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        var characters = MemoryMarshal.Cast<byte, char>(bytes.AsSpan());
        var terminator = characters.IndexOf('\0');
        return new string(terminator >= 0 ? characters[..terminator] : characters);
    }

    private static string? DecodeHtml(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        var terminator = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, terminator >= 0 ? terminator : bytes.Length);
    }

    private static unsafe IReadOnlyList<string> ReadFileDrop()
    {
        var handle = GetClipboardData(FileDropFormat);
        if (handle == IntPtr.Zero)
        {
            return [];
        }

        var paths = new List<string>();
        var count = DragQueryFile(handle, uint.MaxValue, null, 0);
        for (uint index = 0; index < count; index++)
        {
            var length = DragQueryFile(handle, index, null, 0);
            var buffer = new char[checked((int)length + 1)];
            uint copied;
            fixed (char* fileName = buffer)
            {
                copied = DragQueryFile(handle, index, fileName, (uint)buffer.Length);
            }

            if (copied > 0)
            {
                paths.Add(new string(buffer, 0, (int)copied));
            }
        }

        return paths;
    }

    private static byte[] EncodeFileDrop(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            // DROPFILES cannot represent an empty path or an embedded NUL.
            _ = Path.GetFullPath(path);
        }

        var fileList = string.Join('\0', paths) + "\0\0";
        var data = new byte[checked(DropFilesHeaderSize + Encoding.Unicode.GetByteCount(fileList))];
        BinaryPrimitives.WriteUInt32LittleEndian(data, DropFilesHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(DropFilesWideFlagOffset), 1);
        Encoding.Unicode.GetBytes(fileList.AsSpan(), data.AsSpan(DropFilesHeaderSize));
        return data;
    }

    private static ClipboardHandle CreateDibBlock(byte[] bitmapBytes)
    {
        using var source = new MemoryStream(bitmapBytes);
        using var bitmap = new Bitmap(source);
        using var bmp = new MemoryStream();
        bitmap.Save(bmp, ImageFormat.Bmp);

        // CF_DIB contains the BMP information header and pixels without the file header.
        return CreateMemoryBlock(DibFormat, bmp.GetBuffer(), BitmapFileHeaderSize, checked((int)bmp.Length - BitmapFileHeaderSize));
    }

    private static ClipboardHandle CreateMemoryBlock(uint format, byte[] bytes, int offset = 0, int? length = null)
    {
        var count = length ?? (bytes.Length - offset);
        var handle = GlobalAlloc(MoveableMemory, checked((nuint)count));
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        try
        {
            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            try
            {
                Marshal.Copy(bytes, offset, pointer, count);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            return new ClipboardHandle(format, handle);
        }
        catch
        {
            GlobalFree(handle);
            throw;
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial IntPtr CreateWindowEx(uint extendedStyle, string className, string? windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(IntPtr window);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetClipboardData(uint format);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetClipboardData(uint format, IntPtr data);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint RegisterClipboardFormat(string format);

    [LibraryImport("shell32.dll", EntryPoint = "DragQueryFileW")]
    private static unsafe partial uint DragQueryFile(IntPtr drop, uint index, char* fileName, uint fileNameSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalAlloc(uint flags, nuint size);

    [LibraryImport("kernel32.dll")]
    private static partial nuint GlobalSize(IntPtr handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalLock(IntPtr handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr handle);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalFree(IntPtr handle);

    private readonly record struct ClipboardHandle(uint Format, IntPtr Handle);
}
