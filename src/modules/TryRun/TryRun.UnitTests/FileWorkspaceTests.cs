// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class FileWorkspaceTests
{
    [TestMethod]
    public void CopiesNestedInputsAndComparesContentWithoutChangingOriginals()
    {
        using var source = new RunSession();
        using var session = new RunSession();
        var folder = Directory.CreateDirectory(Path.Combine(source.WorkingDirectory, "input")).FullName;
        File.WriteAllText(Path.Combine(folder, "modified.txt"), "before");
        File.WriteAllText(Path.Combine(folder, "deleted.txt"), "keep original");
        File.WriteAllBytes(Path.Combine(folder, "binary.bin"), [0, 255, 128, 1]);
        Directory.CreateDirectory(Path.Combine(folder, "empty"));
        var workspace = new FileWorkspace(session);
        workspace.Import([folder], CancellationToken.None);
        var copied = Path.Combine(session.WorkingDirectory, "input");
        Assert.IsTrue(Directory.Exists(Path.Combine(copied, "empty")));
        File.WriteAllText(Path.Combine(copied, "modified.txt"), "after!");
        File.Delete(Path.Combine(copied, "deleted.txt"));
        File.WriteAllText(Path.Combine(copied, "added.txt"), "new");
        var changes = workspace.Review(CancellationToken.None).ToDictionary(change => change.RelativePath);
        Assert.AreEqual(FileChangeKind.Modified, changes["input\\modified.txt"].Kind);
        Assert.AreEqual("before", changes["input\\modified.txt"].Before!.Preview);
        Assert.AreEqual("after!", changes["input\\modified.txt"].After!.Preview);
        Assert.AreEqual(FileChangeKind.Added, changes["input\\added.txt"].Kind);
        Assert.AreEqual(FileChangeKind.Deleted, changes["input\\deleted.txt"].Kind);
        Assert.AreEqual(FileChangeKind.Unchanged, changes["input\\binary.bin"].Kind);
        Assert.AreEqual("before", File.ReadAllText(Path.Combine(folder, "modified.txt")));
        Assert.AreEqual("keep original", File.ReadAllText(Path.Combine(folder, "deleted.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(folder, "added.txt")));
    }

    [TestMethod]
    public void ExportsOnlySelectedFilesToDistinctNewDirectories()
    {
        using var session = new RunSession();
        using var destination = new RunSession();
        var workspace = new FileWorkspace(session);
        workspace.Import([], CancellationToken.None);
        Directory.CreateDirectory(Path.Combine(session.WorkingDirectory, "nested"));
        File.WriteAllBytes(Path.Combine(session.WorkingDirectory, "nested", "result.bin"), [0, 2, 255]);
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "ignored.txt"), "ignore");
        File.WriteAllText(Path.Combine(destination.WorkingDirectory, "keep.txt"), "keep");
        workspace.Review(CancellationToken.None);
        var first = workspace.Export(destination.WorkingDirectory, ["nested/result.bin"], CancellationToken.None);
        var second = workspace.Export(destination.WorkingDirectory, ["nested/result.bin"], CancellationToken.None);
        Assert.AreNotEqual(first, second);
        CollectionAssert.AreEqual(new byte[] { 0, 2, 255 }, File.ReadAllBytes(Path.Combine(first, "nested", "result.bin")));
        Assert.IsFalse(File.Exists(Path.Combine(first, "ignored.txt")));
        Assert.AreEqual("keep", File.ReadAllText(Path.Combine(destination.WorkingDirectory, "keep.txt")));
    }

    [TestMethod]
    public void RejectsExportAfterWorkspaceChangesAndAllowsFreshReview()
    {
        using var session = new RunSession();
        using var destination = new RunSession();
        var workspace = new FileWorkspace(session);
        workspace.Import([], CancellationToken.None);
        var file = Path.Combine(session.WorkingDirectory, "result.txt");
        File.WriteAllText(file, "before");
        workspace.Review(CancellationToken.None);
        File.WriteAllText(file, "after!");
        Assert.ThrowsException<IOException>(() => workspace.Export(destination.WorkingDirectory, ["result.txt"], CancellationToken.None));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(destination.WorkingDirectory).Any());
        workspace.Review(CancellationToken.None);
        var exported = workspace.Export(destination.WorkingDirectory, ["result.txt"], CancellationToken.None);
        Assert.AreEqual("after!", File.ReadAllText(Path.Combine(exported, "result.txt")));
    }

    [TestMethod]
    public void RejectsDuplicateInputNamesAndSecondImport()
    {
        using var first = new RunSession();
        using var second = new RunSession();
        using var session = new RunSession();
        File.WriteAllText(Path.Combine(first.WorkingDirectory, "same.txt"), "first");
        File.WriteAllText(Path.Combine(second.WorkingDirectory, "same.txt"), "second");
        var workspace = new FileWorkspace(session);
        Assert.ThrowsException<IOException>(() => workspace.Import([Path.Combine(first.WorkingDirectory, "same.txt"), Path.Combine(second.WorkingDirectory, "same.txt")], CancellationToken.None));
        Assert.AreEqual("first", File.ReadAllText(Path.Combine(session.WorkingDirectory, "same.txt")));
        Assert.ThrowsException<InvalidOperationException>(() => workspace.Review(CancellationToken.None));
    }

    [TestMethod]
    public void RejectsOversizedFilesAndTooManyEntries()
    {
        using var source = new RunSession();
        using var session = new RunSession();
        var file = Path.Combine(source.WorkingDirectory, "large.bin");
        using (var stream = File.Create(file))
        {
            stream.SetLength(WorkspacePath.MaximumFileBytes + 1);
        }

        var workspace = new FileWorkspace(session);
        Assert.ThrowsException<IOException>(() => workspace.Import([file], CancellationToken.None));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(session.WorkingDirectory).Any());
        workspace.Import([], CancellationToken.None);
        for (var index = 0; index <= WorkspacePath.MaximumEntries; index++)
        {
            Directory.CreateDirectory(Path.Combine(session.WorkingDirectory, $"folder{index}"));
        }

        Assert.ThrowsException<IOException>(() => workspace.Review(CancellationToken.None));
    }

    [TestMethod]
    public void RejectsExcessiveTotalSizeAndDepth()
    {
        using var session = new RunSession();
        var workspace = new FileWorkspace(session);
        workspace.Import([], CancellationToken.None);
        for (var index = 0; index < 4; index++)
        {
            using var stream = File.Create(Path.Combine(session.WorkingDirectory, $"large{index}.bin"));
            stream.SetLength(WorkspacePath.MaximumFileBytes);
        }

        Assert.ThrowsException<IOException>(() => workspace.Review(CancellationToken.None));
        Assert.ThrowsException<ArgumentException>(() => WorkspacePath.ValidateRelative(string.Join('\\', Enumerable.Repeat("folder", WorkspacePath.MaximumDepth + 1))));
    }

    [TestMethod]
    public void RejectsHardLinksAndDirectoryJunctions()
    {
        using var source = new RunSession();
        using var session = new RunSession();
        var original = Path.Combine(source.WorkingDirectory, "original.txt");
        var hardLink = Path.Combine(source.WorkingDirectory, "linked.txt");
        File.WriteAllText(original, "original");
        Assert.IsTrue(CreateHardLink(hardLink, original, IntPtr.Zero), $"Hard link setup failed: {Marshal.GetLastWin32Error()}");
        var workspace = new FileWorkspace(session);
        Assert.ThrowsException<IOException>(() => workspace.Import([hardLink], CancellationToken.None));
        Assert.ThrowsException<IOException>(() => TaskBundle.Inspect([hardLink], CancellationToken.None));
        Assert.ThrowsException<IOException>(() => RunArtifacts.ReadFile(hardLink, source.WorkingDirectory, 4096));
        var junction = Path.Combine(session.TemporaryDirectory, "linked-folder");
        CreateJunction(junction, source.WorkingDirectory);
        Assert.ThrowsException<IOException>(() => TaskBundle.Inspect([junction], CancellationToken.None));
        Assert.ThrowsException<IOException>(() => TaskBundle.Inspect([Path.Combine(junction, "original.txt")], CancellationToken.None));
        Assert.ThrowsException<IOException>(() => workspace.Import([Path.Combine(junction, "original.txt")], CancellationToken.None));
        workspace.Import([], CancellationToken.None);
        CreateJunction(Path.Combine(session.WorkingDirectory, "escape"), source.WorkingDirectory);
        Assert.ThrowsException<IOException>(() => RunSession.ValidateWorkspacePath(Path.Combine(session.WorkingDirectory, "escape"), session.WorkingDirectory));
        Assert.ThrowsException<IOException>(() => RunArtifacts.ReadFile(Path.Combine(session.WorkingDirectory, "escape", "original.txt"), session.WorkingDirectory, 4096));
        Assert.ThrowsException<IOException>(() => workspace.Review(CancellationToken.None));
        Assert.AreEqual("original", File.ReadAllText(original));
    }

    [TestMethod]
    public void RejectsExportIntoSessionOrThroughJunction()
    {
        using var session = new RunSession();
        using var destination = new RunSession();
        var workspace = new FileWorkspace(session);
        workspace.Import([], CancellationToken.None);
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "result.txt"), "result");
        workspace.Review(CancellationToken.None);
        Assert.ThrowsException<IOException>(() => workspace.Export(session.TemporaryDirectory, ["result.txt"], CancellationToken.None));
        var junction = Path.Combine(destination.TemporaryDirectory, "export-link");
        CreateJunction(junction, destination.WorkingDirectory);
        Assert.ThrowsException<IOException>(() => workspace.Export(junction, ["result.txt"], CancellationToken.None));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(destination.WorkingDirectory).Any());
    }

    [TestMethod]
    public void CancellationAndInvalidSelectionsDoNotCreateExports()
    {
        using var session = new RunSession();
        using var destination = new RunSession();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var workspace = new FileWorkspace(session);
        workspace.Import([], CancellationToken.None);
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "file.txt"), "result");
        workspace.Review(CancellationToken.None);
        Assert.ThrowsException<OperationCanceledException>(() => workspace.Export(destination.WorkingDirectory, ["file.txt"], canceled.Token));
        Assert.ThrowsException<ArgumentException>(() => workspace.Export(destination.WorkingDirectory, ["..\\escape.txt"], CancellationToken.None));
        Assert.ThrowsException<ArgumentException>(() => workspace.Export(destination.WorkingDirectory, ["missing.txt"], CancellationToken.None));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(destination.WorkingDirectory).Any());
        Assert.ThrowsException<InvalidOperationException>(() => workspace.Import([], CancellationToken.None));
    }

    [TestMethod]
    [DataRow("..\\escape.txt")]
    [DataRow("C:\\escape.txt")]
    [DataRow("\\\\server\\file")]
    [DataRow("file.txt:stream")]
    [DataRow("folder\\..\\file")]
    [DataRow("folder\\\\file")]
    [DataRow("file. ")]
    [DataRow("NUL.txt")]
    [DataRow("COM¹.txt")]
    public void RejectsUnsafeRelativePaths(string path)
    {
        Assert.ThrowsException<ArgumentException>(() => WorkspacePath.ValidateRelative(path));
    }

    [TestMethod]
    public void PreviewsAreBoundedAndHandleUnicodeAndBinary()
    {
        Assert.AreEqual("你好", WorkspaceSnapshot.FormatPreview(Encoding.UTF8.GetBytes("你好"), false));
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("你好")).ToArray();
        Assert.AreEqual("你好", WorkspaceSnapshot.FormatPreview(utf16, false));
        StringAssert.Contains(WorkspaceSnapshot.FormatPreview([0, 255, 1], false), "Binary");
        var text = WorkspaceSnapshot.FormatPreview(Encoding.UTF8.GetBytes(new string('a', 9000)), false);
        Assert.IsTrue(text.Length < 8300);
        StringAssert.Contains(text, "Preview limited");
    }

    private static void CreateJunction(string path, string target)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add($"New-Item -ItemType Junction -Path '{path.Replace("'", "''", StringComparison.Ordinal)}' -Target '{target.Replace("'", "''", StringComparison.Ordinal)}' -ErrorAction Stop | Out-Null");
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEnd();
        Assert.IsTrue(process.WaitForExit(10000));
        Assert.AreEqual(0, process.ExitCode, error);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}
