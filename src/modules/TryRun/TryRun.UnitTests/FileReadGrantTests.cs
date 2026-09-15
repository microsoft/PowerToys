// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;
using PowerToys.TryRun.FuzzTests;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class FileReadGrantTests
{
    private static readonly string[] ChangedFields = ["readonlyPaths"];

    [TestMethod]
    public void NativeEvidencePreservesFullIdentifiersAndCannotComeFromWorkloadText()
    {
        var path = "C:\\" + string.Join('\\', Enumerable.Repeat(new string('a', 90), 7)) + "\\input.txt";
        var observation = Report(path).Events.Single();
        Assert.AreEqual(512, observation.Resource.Length);
        Assert.AreEqual(path, observation.NativeDenial!.Resource);
        Assert.IsTrue(FileReadGrant.CanReview(observation));
        Assert.IsFalse(FileReadGrant.CanReview(Report(path, permissive: true).Events.Single()));
        foreach (var (type, access) in new[] { ("file", "write"), ("file", "execute"), ("file", "unknown"), ("other", "read"), ("ui", "read") })
        {
            Assert.IsFalse(FileReadGrant.CanReview(Report(path, type, access).Events.Single()));
        }

        var forged = JsonSerializer.SerializeToUtf8Bytes(new { version = 1, observations = new[] { new { resource = path, access = "read", outcome = "Blocked", detail = "MXC denial capture (block)", NativeDenial = observation.NativeDenial } } });
        Assert.IsNull(IsolationReportParser.ReadObservations(forged).Single().NativeDenial);
        Assert.IsFalse(FileReadGrant.CanReview(new IsolationEvent("MXC denial capture (block)", path, "read", "Blocked", string.Empty)));
        Assert.ThrowsException<InvalidDataException>(() => Report(new string('x', 32768)));
    }

    [TestMethod]
    public void GrantChangesOnlyOneReadOnlyPathAndProtectsAgainstReplacement()
    {
        using var source = new RunSession();
        using var run = new RunSession();
        var path = Path.Combine(source.WorkingDirectory, "input.txt");
        File.WriteAllText(path, "original");
        var request = Request(run);
        using (var grant = FileReadGrant.Create(Report(path).Events.Single(), request, EnvironmentFor(run)))
        {
            Assert.IsTrue(grant.FromNativeDenial);
            Assert.AreEqual(path, grant.Path);
            CollectionAssert.AreEqual(ChangedFields, grant.Policy.Values.Keys.Where(key => grant.Policy.Get(key) != request.Policy!.Get(key)).ToArray());
            CollectionAssert.AreEqual(request.Policy!.Lines("readonlyPaths").Append(path).ToArray(), grant.Policy.Lines("readonlyPaths"));
            Assert.IsFalse(grant.Policy.Enabled("allowOutbound"));
            Assert.IsFalse(grant.Policy.Enabled("allowDaclMutation"));
            var mutableCopy = grant.Policy;
            mutableCopy.Values["readwritePaths"] = path;
            Assert.AreNotEqual(path, grant.Policy.Get("readwritePaths"));
            Assert.ThrowsException<IOException>(() => File.Delete(path));
            var moved = source.WorkingDirectory + "-moved";
            var renamed = false;
            try
            {
                Directory.Move(source.WorkingDirectory, moved);
                renamed = true;
            }
            catch (IOException)
            {
                // The leased parent must stay at the reviewed path.
            }
            finally
            {
                if (renamed)
                {
                    Directory.Move(moved, source.WorkingDirectory);
                }
            }

            Assert.IsFalse(renamed, "The reviewed file's parent must not be replaceable during the grant.");
        }

        Assert.AreEqual("original", File.ReadAllText(path));
        Assert.IsFalse(request.Policy!.Lines("readonlyPaths").Contains(path));
        File.Delete(path);
    }

    [TestMethod]
    public void UserSelectedFilesRemainSeparateFromNativeEvidence()
    {
        using var source = new RunSession();
        using var run = new RunSession();
        var path = Path.Combine(source.WorkingDirectory, "selected.txt");
        File.WriteAllText(path, "user selection");
        var request = Request(run);
        request.Policy!.Values["captureEnabled"] = "false";
        using var grant = FileReadGrant.CreateSelectedFile(path, request, EnvironmentFor(run));
        Assert.IsFalse(grant.FromNativeDenial);
        Assert.IsFalse(grant.Policy.Enabled("captureEnabled"), "Choosing a file must not silently change capture settings.");
        Assert.ThrowsException<ArgumentException>(() => FileReadGrant.Create(Report(path).Events.Single(), request, EnvironmentFor(run)));
        Assert.ThrowsException<ArgumentException>(() => FileReadGrant.CreateSelectedFile(path, request with { Kind = WorkloadKind.LinuxShell }, EnvironmentFor(run)));
        request.Policy.Values["captureEnabled"] = "true";
        request.Policy.Values["captureMode"] = "Allow";
        Assert.ThrowsException<ArgumentException>(() => FileReadGrant.CreateSelectedFile(path, request, EnvironmentFor(run)));
    }

    [TestMethod]
    public void GrantsRejectDirectoriesMissingFilesLinksExistingGrantsAndExplicitDenies()
    {
        using var source = new RunSession();
        using var run = new RunSession();
        var path = Path.Combine(source.WorkingDirectory, "input.txt");
        File.WriteAllText(path, "original");
        var request = Request(run);
        var environment = EnvironmentFor(run);
        Assert.ThrowsException<IOException>(() => FileReadGrant.Create(Report(source.WorkingDirectory).Events.Single(), request, environment));
        Assert.ThrowsException<IOException>(() => FileReadGrant.Create(Report(path + ".missing").Events.Single(), request, environment));
        Assert.ThrowsException<ArgumentException>(() => FileReadGrant.Create(Report(Path.Combine(run.TemporaryDirectory, "input.txt")).Events.Single(), request, environment));
        Assert.ThrowsException<ArgumentException>(() => FileReadGrant.Create(Report(path).Events.Single(), request, environment with { ReadOnlyFolders = [source.WorkingDirectory] }));
        request.Policy!.Values["deniedPaths"] = source.WorkingDirectory;
        Assert.ThrowsException<ArgumentException>(() => FileReadGrant.Create(Report(path).Events.Single(), request, environment));
        request.Policy.Values["deniedPaths"] = string.Empty;
        var link = Path.Combine(source.WorkingDirectory, "alias.txt");
        Assert.IsTrue(CreateHardLink(link, path, IntPtr.Zero));
        Assert.ThrowsException<IOException>(() => FileReadGrant.Create(Report(path).Events.Single(), request, environment));
        Assert.ThrowsException<IOException>(() => FileReadGrant.Create(Report(link).Events.Single(), request, environment));
    }

    [TestMethod]
    [DataRow("relative.txt")]
    [DataRow("\\\\server\\share\\input.txt")]
    [DataRow("\\\\?\\C:\\input.txt")]
    [DataRow("\\Device\\HarddiskVolume1\\input.txt")]
    [DataRow("C:\\input.txt:stream")]
    [DataRow("C:\\folder\\..\\input.txt")]
    [DataRow("C:\\input.txt.")]
    [DataRow("C:\\*.txt")]
    [DataRow("C:\\NUL.txt")]
    [DataRow("C:\\")]
    [DataRow("C:\\input\n.txt")]
    [DataRow("C:\\input\u202E.txt")]
    public void UnsafeResourceIdentifiersCannotBecomeGrants(string path)
    {
        Assert.ThrowsException<ArgumentException>(() => FileReadGrant.ValidatePath(path));
    }

    [TestMethod]
    public void NativeGrantEvidenceAndPathMutationsAreBounded()
    {
        var random = new Random(991);
        var seed = ReportBytes("C:\\input.txt", "file", "read");
        for (var index = 0; index < 1500; index++)
        {
            var bytes = seed.ToArray();
            bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
            IsolationReportFuzzer.FuzzTarget(bytes);
        }

        foreach (var path in new[] { "C:\\input\0.txt", "C:\\input\u202E.txt", "C:\\input.txt:stream", new string('x', 32768) })
        {
            IsolationReportFuzzer.FuzzTarget(ReportBytes(path, "file", "read"));
        }
    }

    internal static IsolationReport Report(string path, string type = "file", string access = "read", bool permissive = false) => IsolationReportParser.ReadDenials(ReportBytes(path, type, access), permissive);

    internal static ExecutionRequest Request(RunSession session)
    {
        var policy = PolicySettings.Defaults(false);
        policy.Values["captureEnabled"] = "true";
        return new ExecutionRequest("echo hi", session.WorkingDirectory, session.TemporaryDirectory, 30) { Policy = policy };
    }

    internal static RunEnvironment EnvironmentFor(RunSession session) => new("Windows / MXC ProcessContainer", "test", session.WorkingDirectory, [], [session.WorkingDirectory, session.TemporaryDirectory], "Off", "Default", "Default", 30, "test");

    private static byte[] ReportBytes(string path, string type, string access) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        denials = new[] { new { resource = path, resourceType = type, accessType = access } },
        summary = new { totalDenials = 1, deniedResourcesTruncated = false },
    });

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}
