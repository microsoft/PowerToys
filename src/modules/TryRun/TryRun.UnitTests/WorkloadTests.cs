// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class WorkloadTests
{
    [TestMethod]
    public void WindowsArgumentsRoundTripThroughWindowsParser()
    {
        var arguments = new[] { "program.exe", string.Empty, "hello world", "quote\"here", "C:\\trailing path\\", "literal&$()", "back\\\\\"slash" };
        var command = string.Join(" ", arguments.Select(CommandEncoding.WindowsArgument));
        var pointer = CommandLineToArgv(command, out var count);
        Assert.AreNotEqual(IntPtr.Zero, pointer);
        try
        {
            var actual = Enumerable.Range(0, count).Select(index => Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, index * IntPtr.Size))).ToArray();
            CollectionAssert.AreEqual(arguments, actual);
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    [TestMethod]
    public void LinuxPathsUseOnlyTheRequestedHostDrive()
    {
        Assert.AreEqual("/mnt/c/a folder/file.txt", CommandEncoding.LinuxPath("C:\\a folder\\file.txt"));
        Assert.ThrowsException<ArgumentException>(() => CommandEncoding.LinuxPath("\\\\server\\share"));
        Assert.ThrowsException<ArgumentException>(() => CommandEncoding.LinuxPath("file.txt"));
    }

    [TestMethod]
    public void RejectsMismatchedWorkloadsAndUnsafeArguments()
    {
        var request = new ExecutionRequest("echo hello", "C:\\work", "C:\\temp", 30);
        Assert.ThrowsException<ArgumentException>(() => (request with { Kind = (WorkloadKind)99 }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { Kind = WorkloadKind.WindowsApplication, ApplicationPath = "C:\\script.cmd" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { Kind = WorkloadKind.LinuxApplication }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { Kind = WorkloadKind.LinuxShell, Image = "alpine; echo injected" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { Kind = WorkloadKind.WindowsBatch, Arguments = ["%PATH%"] }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { FileRelativePath = "..\\outside.ps1" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { FileRelativePath = "document.txt" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { Kind = WorkloadKind.WindowsBatch, FileRelativePath = "program.exe" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { Arguments = ["null\0byte"] }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { PrepareImage = true }).Validate());
        (request with { Script = string.Empty, Kind = WorkloadKind.WindowsApplication, ApplicationPath = "C:\\app.exe" }).Validate();
    }

    [DllImport("shell32.dll", EntryPoint = "CommandLineToArgvW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgv(string command, out int count);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
