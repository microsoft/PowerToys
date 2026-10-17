// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
[SupportedOSPlatform("windows")]
public class TrayIconAssetsTests
{
    private static readonly int[] RequiredResolutions = [16, 24, 32, 48, 128];

    [DataRow("disabled.ico")]
    [DataRow("expirable.ico")]
    [DataRow("indefinite.ico")]
    [DataRow("timed.ico")]
    [DataTestMethod]
    public void TrayIcon_CompressedFrames_LoadAtOriginalResolutions(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Awake", fileName);
        using (FileStream stream = File.OpenRead(path))
        {
            AssertRequiredFrames(stream);
        }

        foreach (int resolution in RequiredResolutions)
        {
            using Icon icon = new(path, resolution, resolution);
            Assert.AreEqual(resolution, icon.Width);
            Assert.AreEqual(resolution, icon.Height);
            Assert.AreNotEqual(IntPtr.Zero, icon.Handle);
        }
    }

    [DataRow(16)]
    [DataRow(24)]
    [DataRow(32)]
    [DataRow(48)]
    [DataRow(128)]
    [DataTestMethod]
    public void TrayIcon_DirectoryMissingRequiredFrame_Fails(int resolution)
    {
        using MemoryStream stream = CreateIconDirectory(resolution, omitFrame: true);
        Assert.ThrowsException<AssertFailedException>(() => AssertRequiredFrames(stream));
    }

    [DataRow(16)]
    [DataRow(24)]
    [DataRow(32)]
    [DataRow(48)]
    [DataRow(128)]
    [DataTestMethod]
    public void TrayIcon_DirectoryRequiredFrameAt24BitsPerPixel_Fails(int resolution)
    {
        using MemoryStream stream = CreateIconDirectory(resolution, omitFrame: false);
        Assert.ThrowsException<AssertFailedException>(() => AssertRequiredFrames(stream));
    }

    private static void AssertRequiredFrames(Stream stream)
    {
        using BinaryReader reader = new(stream);
        Assert.AreEqual((ushort)0, reader.ReadUInt16(), "ICO reserved field must be zero.");
        Assert.AreEqual((ushort)1, reader.ReadUInt16(), "Asset must be an ICO file.");
        ushort count = reader.ReadUInt16();
        HashSet<(int Width, int Height, int BitsPerPixel)> frames = [];
        for (int index = 0; index < count; index++)
        {
            int width = reader.ReadByte();
            int height = reader.ReadByte();
            reader.ReadByte(); // Color count.
            reader.ReadByte(); // Reserved.
            reader.ReadUInt16(); // Color planes.
            int bitsPerPixel = reader.ReadUInt16();
            reader.ReadUInt32(); // Image byte count.
            reader.ReadUInt32(); // Image offset.
            frames.Add((width == 0 ? 256 : width, height == 0 ? 256 : height, bitsPerPixel));
        }

        foreach (int resolution in RequiredResolutions)
        {
            Assert.IsTrue(frames.Contains((resolution, resolution, 32)), $"ICO must contain a {resolution}x{resolution} frame at 32 bits per pixel.");
        }
    }

    private static MemoryStream CreateIconDirectory(int changedResolution, bool omitFrame)
    {
        MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)(RequiredResolutions.Length - (omitFrame ? 1 : 0)));
            foreach (int resolution in RequiredResolutions)
            {
                if (omitFrame && resolution == changedResolution)
                {
                    continue;
                }

                writer.Write((byte)resolution);
                writer.Write((byte)resolution);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)(resolution == changedResolution ? 24 : 32));
                writer.Write(0u);
                writer.Write(0u);
            }
        }

        stream.Position = 0;
        return stream;
    }
}
