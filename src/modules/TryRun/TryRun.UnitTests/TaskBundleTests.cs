// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class TaskBundleTests
{
    [TestMethod]
    public void FolderSelectionDeduplicatesChildrenAndPreservesStructure()
    {
        using var source = new RunSession();
        using var target = new RunSession();
        var folder = Path.Combine(source.WorkingDirectory, "a package");
        Directory.CreateDirectory(Path.Combine(folder, "nested"));
        var script = Path.Combine(folder, "nested", "run.ps1");
        File.WriteAllText(script, "throw 'Inspection must never execute me'");
        File.WriteAllText(Path.Combine(folder, "nested", "data.txt"), "original");
        var bundle = TaskBundle.Inspect([script, folder, folder.ToUpperInvariant()], CancellationToken.None);
        Assert.AreEqual(1, bundle.Inputs.Count);
        Assert.AreEqual(4, bundle.EntryCount);
        var entry = bundle.EntryPoints.Single();
        Assert.AreEqual("a package\\nested\\run.ps1", entry.RelativePath);
        Assert.AreEqual("a package\\nested", entry.WorkingSubdirectory);
        Assert.AreEqual(entry.RelativePath, bundle.GetRelativePath(script));
        var workspace = new FileWorkspace(target);
        workspace.Import(bundle.Inputs, CancellationToken.None);
        Assert.AreEqual("original", File.ReadAllText(Path.Combine(target.WorkingDirectory, "a package", "nested", "data.txt")));
        Assert.AreEqual(2, workspace.Review(CancellationToken.None).Count);
    }

    [TestMethod]
    public void LooseSelectionsDoNotIncludeUnselectedNeighbors()
    {
        using var source = new RunSession();
        var script = Path.Combine(source.WorkingDirectory, "run.sh");
        File.WriteAllText(script, "echo hello");
        File.WriteAllText(Path.Combine(source.WorkingDirectory, "private.txt"), "not selected");
        var bundle = TaskBundle.Inspect([script], CancellationToken.None);
        Assert.AreEqual(1, bundle.EntryCount);
        Assert.AreEqual("run.sh", bundle.EntryPoints.Single().RelativePath);
        Assert.IsNull(bundle.EntryPoints.Single().WorkingSubdirectory);
        Assert.ThrowsException<ArgumentException>(() => bundle.GetRelativePath(Path.Combine(source.WorkingDirectory, "private.txt")));
    }

    [TestMethod]
    public void AmbiguousAndDataOnlySelectionsDoNotInventAnEntryPoint()
    {
        using var source = new RunSession();
        var data = Path.Combine(source.WorkingDirectory, "notes.txt");
        File.WriteAllText(data, "hello");
        Assert.AreEqual(0, TaskBundle.Inspect([data], CancellationToken.None).EntryPoints.Count);
        File.WriteAllText(Path.Combine(source.WorkingDirectory, "first.ps1"), "echo first");
        File.WriteAllText(Path.Combine(source.WorkingDirectory, "second.cmd"), "echo second");
        Assert.AreEqual(2, TaskBundle.Inspect([source.WorkingDirectory], CancellationToken.None).EntryPoints.Count);
    }

    [TestMethod]
    public void RejectsCollidingRootsAndMissingFiles()
    {
        using var first = new RunSession();
        using var second = new RunSession();
        var one = Path.Combine(first.WorkingDirectory, "data.txt");
        var two = Path.Combine(second.WorkingDirectory, "data.txt");
        File.WriteAllText(one, "one");
        File.WriteAllText(two, "two");
        Assert.ThrowsException<IOException>(() => TaskBundle.Inspect([one, two], CancellationToken.None));
        Assert.ThrowsException<FileNotFoundException>(() => TaskBundle.Inspect([Path.Combine(first.WorkingDirectory, "missing")], CancellationToken.None));
    }

    [TestMethod]
    public void InspectionHonorsCancellationAndSizeLimits()
    {
        using var source = new RunSession();
        var file = Path.Combine(source.WorkingDirectory, "huge.bin");
        using (var stream = File.Create(file))
        {
            stream.SetLength(WorkspacePath.MaximumFileBytes + 1);
        }

        Assert.ThrowsException<IOException>(() => TaskBundle.Inspect([file], CancellationToken.None));
        Assert.ThrowsException<OperationCanceledException>(() => TaskBundle.Inspect([file], new CancellationToken(true)));
        Assert.ThrowsException<ArgumentException>(() => TaskBundle.ParseLaunchArguments(Enumerable.Repeat(file, 1001)));
    }

    [TestMethod]
    public void StartupArgumentsArePathsNeverCommands()
    {
        var expected = new[] { "C:\\a folder\\run.ps1", "C:\\data.txt" };
        CollectionAssert.AreEqual(expected, TaskBundle.ParseLaunchArguments(["--", "C:\\a folder\\run.ps1", "C:\\data.txt"]));
        foreach (var argument in new[] { "--run", "-Command", "relative.ps1", "\\\\server\\share", "C:\\file:stream", "C:\\bad\0path" })
        {
            Assert.ThrowsException<ArgumentException>(() => TaskBundle.ParseLaunchArguments([argument]));
        }
    }

    [TestMethod]
    public void HeaderDetectionIsBoundedAndDoesNotEvaluateShebangs()
    {
        Assert.AreEqual(WorkloadKind.WindowsPowerShell, EntryPointDetector.Detect("RUN.PS1", [])!.Kind);
        Assert.AreEqual("bash", EntryPointDetector.Detect("run.sh", "#!/bin/bash\necho hi"u8)!.Interpreter);
        Assert.AreEqual(WorkloadKind.LinuxPython, EntryPointDetector.Detect("run", "#!/usr/bin/env python3\n"u8)!.Kind);
        Assert.IsNull(EntryPointDetector.Detect("run", "#!/usr/bin/env -S sh -c bad\n"u8));
        Assert.IsNull(EntryPointDetector.Detect("fake.exe", "not executable"u8));
        var header = new byte[128];
        "MZ"u8.CopyTo(header);
        header[60] = 64;
        "PE\0\0"u8.CopyTo(header.AsSpan(64));
        Assert.AreEqual(WorkloadKind.WindowsApplication, EntryPointDetector.Detect("app.exe", header)!.Kind);
        header[87] = 0x20;
        Assert.IsNull(EntryPointDetector.Detect("library.exe", header));
        header[60] = 255;
        Assert.IsNull(EntryPointDetector.Detect("bad.exe", header));
        var elf = new byte[64];
        "\u007fELF"u8.CopyTo(elf);
        elf[4] = 2;
        elf[5] = 1;
        elf[16] = 2;
        Assert.AreEqual(WorkloadKind.LinuxApplication, EntryPointDetector.Detect("tool", elf)!.Kind);
        Assert.IsNull(EntryPointDetector.Detect("library.so", elf));
    }

    [TestMethod]
    public void CopiedApplicationAndWorkingFolderCannotEscapeTheWorkspace()
    {
        using var session = new RunSession();
        var request = new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsApplication,
            FileRelativePath = "package\\app.exe",
            WorkingSubdirectory = "package",
        };
        request.Validate();
        Assert.ThrowsException<ArgumentException>(() => (request with { ApplicationPath = "C:\\app.exe" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { FileRelativePath = "..\\app.exe" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { WorkingSubdirectory = "..\\Temp" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { WorkingSubdirectory = "C:\\Windows" }).Validate());
        Assert.ThrowsException<ArgumentException>(() => RunSession.ValidateWorkspacePath(session.TemporaryDirectory, session.WorkingDirectory));
    }
}
