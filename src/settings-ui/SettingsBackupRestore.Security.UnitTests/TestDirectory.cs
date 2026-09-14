// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security.UnitTests;

internal sealed class TestDirectory : IDisposable
{
    private readonly List<string> links = [];

    internal TestDirectory()
    {
        string ownerRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerToys-SettingsBackupRestore-tests");
        Path = System.IO.Path.Combine(ownerRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string CreateDirectory(string relativePath)
    {
        string path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    internal string CreateFile(string relativePath, string contents)
    {
        string path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    internal string CreateDirectoryJunction(string relativePath, string targetPath)
    {
        string linkPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
        ProcessStartInfo startInfo = new()
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            Arguments = $"/d /c mklink /J \"{linkPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, $"mklink failed. stdout: {output} stderr: {error}");
        links.Add(linkPath);
        return linkPath;
    }

    internal string CreateDirectorySymbolicLink(string relativePath, string targetPath)
    {
        string linkPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
        Directory.CreateSymbolicLink(linkPath, targetPath);
        links.Add(linkPath);
        return linkPath;
    }

    internal string CreateHardLink(string relativePath, string targetPath)
    {
        string linkPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
        if (!CreateHardLinkW(linkPath, targetPath, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create test-owned hard link.");
        }

        return linkPath;
    }

    public void Dispose()
    {
        foreach (string link in links)
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
        }

        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }

        string? ownerRoot = System.IO.Path.GetDirectoryName(Path);
        if (ownerRoot != null && Directory.Exists(ownerRoot) && Directory.GetFileSystemEntries(ownerRoot).Length == 0)
        {
            Directory.Delete(ownerRoot);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);
}
