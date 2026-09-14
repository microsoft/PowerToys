// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using PowerToys.TryRun.Core;
using PowerToys.TryRun.Explorer;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class ExplorerTests
{
    [TestMethod]
    public void RegistrationReceiptPathCannotEscapeTheHelperFolder()
    {
        const string directory = "C:\\TryRun\\Explorer";
        var correlation = Guid.NewGuid().ToString("N");
        Assert.AreEqual(Path.Combine(directory, $"TryRun-registration-{correlation}.json"), ExplorerRegistration.GetResultPath(directory, correlation));
        Assert.ThrowsException<ArgumentException>(() => ExplorerRegistration.GetResultPath(directory, "..\\outside"));
        Assert.ThrowsException<ArgumentException>(() => ExplorerRegistration.GetResultPath(directory, "C:\\outside"));
        Assert.ThrowsException<ArgumentException>(() => ExplorerRegistration.GetResultPath(directory, string.Empty));
    }

    [TestMethod]
    public async Task SelectionPipePreservesUnicodeAndLiteralPaths()
    {
        var paths = new[] { "C:\\a folder\\脚本.ps1", "C:\\a folder\\data & value.txt", "C:\\data\\quote'file.txt" };
        var encoded = SelectionPayload.Encode(paths);
        CollectionAssert.AreEqual(paths, await SelectionPayload.ReadAsync(new StringReader(encoded), CancellationToken.None));
        CollectionAssert.AreEqual(paths, SelectionPayload.Decode(encoded));
    }

    [TestMethod]
    public void SelectionPayloadRejectsCommandsAndInvalidVersions()
    {
        foreach (var json in new[] { "null", "{}", "{\"Version\":2,\"Paths\":[\"C:\\\\file\"]}", "{\"Version\":1,\"Paths\":[]}", "{\"Version\":1,\"Paths\":[\"--\",\"C:\\\\file\"]}", "{\"Version\":1,\"Paths\":[\"-Command\"]}" })
        {
            Assert.ThrowsException<ArgumentException>(() => SelectionPayload.Decode(json));
        }

        Assert.ThrowsException<JsonException>(() => SelectionPayload.Decode("{}{}"));
        Assert.ThrowsException<ArgumentException>(() => SelectionPayload.Encode([]));
        Assert.ThrowsException<ArgumentException>(() => SelectionPayload.Decode(new string('x', SelectionPayload.MaximumCharacters + 1)));
    }

    [TestMethod]
    public async Task SelectionReaderIsBoundedAndCancelable()
    {
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => SelectionPayload.ReadAsync(new StringReader("{}"), canceled.Token));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => SelectionPayload.ReadAsync(new StringReader(new string('x', SelectionPayload.MaximumCharacters + 2)), CancellationToken.None));
        var paths = Enumerable.Range(0, 1000).Select(index => $"C:\\data\\{index}.txt").ToArray();
        Assert.AreEqual(1000, SelectionPayload.Decode(SelectionPayload.Encode(paths)).Length);
        Assert.ThrowsException<ArgumentException>(() => SelectionPayload.Encode(paths.Append("C:\\another.txt")));
    }

    [TestMethod]
    public void RegistrationUsesOneOutOfProcessDelegateAndUnregistersOnlyOwnedKeys()
    {
        using var files = new RunSession();
        var application = Path.Combine(files.WorkingDirectory, "PowerToys.TryRun.exe");
        var broker = Path.Combine(files.WorkingDirectory, "Explorer", "PowerToys.TryRun.Explorer.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(broker)!);
        File.WriteAllText(application, "registration fixture");
        File.WriteAllText(broker, "registration fixture");
        var testPath = $"Software\\PowerToys.TryRun.Tests\\{Guid.NewGuid():N}";
        try
        {
            using var classes = Registry.CurrentUser.CreateSubKey(testPath);
            ExplorerRegistration.Register(classes, application, broker);
            ExplorerRegistration.Register(classes, application, broker);
            Assert.IsTrue(ExplorerRegistration.IsRegistered(classes, broker));
            using (var verb = classes.OpenSubKey($"*\\shell\\{ExplorerRegistration.VerbName}"))
            {
                Assert.AreEqual("Player", verb!.GetValue("MultiSelectModel"));
                using var command = verb.OpenSubKey("command");
                Assert.IsNull(command!.GetValue(string.Empty));
            }

            using (var unrelated = classes.CreateSubKey("OtherApplication"))
            {
                unrelated.SetValue("Keep", "unchanged");
            }

            ExplorerRegistration.Unregister(classes);
            ExplorerRegistration.Unregister(classes);
            Assert.IsFalse(ExplorerRegistration.IsRegistered(classes, broker));
            using var kept = classes.OpenSubKey("OtherApplication");
            Assert.AreEqual("unchanged", kept!.GetValue("Keep"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testPath, throwOnMissingSubKey: false);
        }
    }

    [TestMethod]
    public void RegistrationRefusesToOverwriteOrRemoveUnownedKeys()
    {
        var testPath = $"Software\\PowerToys.TryRun.Tests\\{Guid.NewGuid():N}";
        try
        {
            using var classes = Registry.CurrentUser.CreateSubKey(testPath);
            using (var conflicting = classes.CreateSubKey($"*\\shell\\{ExplorerRegistration.VerbName}"))
            {
                conflicting.SetValue(string.Empty, "not owned");
            }

            Assert.ThrowsException<InvalidOperationException>(() => ExplorerRegistration.Unregister(classes));
            using var kept = classes.OpenSubKey($"*\\shell\\{ExplorerRegistration.VerbName}");
            Assert.AreEqual("not owned", kept!.GetValue(string.Empty));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testPath, throwOnMissingSubKey: false);
        }
    }

    [TestMethod]
    public void NativeShellSelectionIsTransferredAsOneList()
    {
        using var source = new RunSession();
        var paths = new[] { Path.Combine(source.WorkingDirectory, "first file.ps1"), Path.Combine(source.WorkingDirectory, "你好 & data.txt"), Path.Combine(source.WorkingDirectory, "folder") };
        File.WriteAllText(paths[0], "throw 'must not execute during selection'");
        File.WriteAllText(paths[1], "original");
        Directory.CreateDirectory(paths[2]);
        var pointers = new List<IntPtr>();
        ShellInterop.IShellItemArray? selection = null;
        try
        {
            foreach (var path in paths)
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out var pointer, 0, out _));
                pointers.Add(pointer);
            }

            Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)pointers.Count, pointers.ToArray(), out selection));
            CollectionAssert.AreEqual(paths, ExplorerCommand.ReadSelection(selection));
            Assert.AreEqual("original", File.ReadAllText(paths[1]));
        }
        finally
        {
            if (selection is not null)
            {
                Marshal.ReleaseComObject(selection);
            }

            foreach (var pointer in pointers)
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }
    }

    [TestMethod]
    public void CommandFactoryExposesTheWindowsVerbInterfaces()
    {
        var command = new ExplorerCommand(_ => Assert.Fail("No selection may run."), () => { });
        var factory = new CommandFactory(command);
        foreach (var type in new[] { typeof(ShellInterop.IExecuteCommand), typeof(ShellInterop.IObjectWithSelection) })
        {
            var interfaceId = type.GUID;
            Assert.AreEqual(0, factory.CreateInstance(IntPtr.Zero, ref interfaceId, out var pointer));
            Assert.AreNotEqual(IntPtr.Zero, pointer);
            Marshal.Release(pointer);
        }

        Assert.IsTrue(command.Execute() < 0);
        command.SetParameters("-Command arbitrary input");
        command.SetDirectory("C:\\Windows");
        Assert.IsTrue(command.Execute() < 0);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr item, uint attributes, out uint actualAttributes);

    [DllImport("shell32.dll")]
    private static extern int SHCreateShellItemArrayFromIDLists(uint count, [In] IntPtr[] items, [MarshalAs(UnmanagedType.Interface)] out ShellInterop.IShellItemArray array);
}
