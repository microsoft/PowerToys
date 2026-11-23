// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json.Nodes;
using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class WinAppSandboxPrerequisiteReportTests
{
    private const string RunId = "d2b96d2f-5b49-42d1-9576-227250294fb0";
    private static readonly string[] StageNames =
    [
        "OperatingSystem", "UserPackageRegistration", "UserExecutionAlias", "PackageExecutable",
        "PrivateToolAndState", "CliSchema", "ProviderVersion",
    ];

    [TestMethod]
    public void UnreachedChecksRecordTheActualIdentityAndExplicitNotCheckedStages()
    {
        WithRunRoot(root =>
        {
            WinAppSandboxPrerequisiteReport.Capture(root, RunId, _ => { });
            var report = RunFiles.Read(Path.Combine(root, WinAppSandboxPrerequisiteReport.FileName));
            using var identity = WindowsIdentity.GetCurrent();
            using var process = Process.GetCurrentProcess();
            Assert.AreEqual(1, report["SchemaVersion"]!.GetValue<int>());
            Assert.AreEqual(RunId, report["RunId"]!.GetValue<string>());
            Assert.AreEqual("CurrentInteractiveUser", report["Scope"]!.GetValue<string>());
            Assert.AreEqual(identity.User!.Value, report["UserSid"]!.GetValue<string>());
            Assert.AreEqual(identity.Name, report["UserAccount"]!.GetValue<string>());
            Assert.AreEqual(process.SessionId, report["SessionId"]!.GetValue<int>());
            Assert.AreEqual(process.Id, report["ProcessId"]!.GetValue<int>());
            Assert.AreEqual(RuntimeInformation.ProcessArchitecture.ToString(), report["ProcessArchitecture"]!.GetValue<string>());
            Assert.AreEqual(RuntimeInformation.OSArchitecture.ToString(), report["OsArchitecture"]!.GetValue<string>());
            Assert.AreEqual(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator), report["IsElevated"]!.GetValue<bool>());
            var stages = report["Stages"]!.AsObject();
            CollectionAssert.AreEquivalent(StageNames, stages.Select(stage => stage.Key).ToArray());
            foreach (var stage in stages)
            {
                Assert.AreEqual("NotChecked", stage.Value!["Status"]!.GetValue<string>());
                Assert.IsNull(stage.Value["FailureCode"]);
                Assert.IsNull(stage.Value["QueryErrorHResult"]);
            }

            Assert.IsNull(stages["UserPackageRegistration"]!["Count"]);
            Assert.AreEqual("NotChecked", stages["UserPackageRegistration"]!["QueryStatus"]!.GetValue<string>());
            Assert.IsNull(stages["UserPackageRegistration"]!["Present"]);
            Assert.IsNull(stages["UserPackageRegistration"]!["Packages"]);
            Assert.IsNull(stages["UserExecutionAlias"]!["Exists"]);
            Assert.IsNull(stages["UserExecutionAlias"]!["IsReparsePoint"]);
            Assert.IsNull(stages["PackageExecutable"]!["Exists"]);
            Assert.IsNull(stages["PackageExecutable"]!["InstalledLocation"]);
            Assert.IsNull(stages["ProviderVersion"]!["VersionLine"]);
            Assert.IsNull(stages["ProviderVersion"]!["ExitCode"]);
            Assert.IsNull(stages["ProviderVersion"]!["TimedOut"]);
        });
    }

    [TestMethod]
    public void FailureBeforeFixtureConstructionStillAttachesPersistentNotCheckedEvidence()
    {
        WithRunRoot(root =>
        {
            var deployment = Directory.CreateDirectory(Path.Combine(root, "deployment")).FullName;
            string? attached = null;
            var failure = new InvalidOperationException("private preflight failure");
            var actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
                WinAppSandboxPrerequisiteReport.CapturePersistent(deployment, path => attached = path, _ =>
                {
                    Assert.AreEqual(1, Directory.GetFiles(root, WinAppSandboxPrerequisiteReport.FileName, SearchOption.AllDirectories).Length);
                    throw failure;
                }));
            Assert.AreSame(failure, actual);
            Assert.IsNotNull(attached);
            Directory.Delete(deployment);
            Assert.IsTrue(File.Exists(attached), "Removing a successful MSTest deployment must not remove diagnostic evidence.");
            var evidence = RunFiles.Read(attached);
            Assert.IsNull(evidence["RunId"], "No protected RunId has been read yet.");
            Assert.IsTrue(Guid.TryParseExact(evidence["InvocationId"]!.GetValue<string>(), "N", out _));
            Assert.IsTrue(evidence["Stages"]!.AsObject().All(stage => stage.Value!["Status"]!.GetValue<string>() == "NotChecked"));
            Assert.IsFalse(evidence.ToJsonString().Contains("private", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void PersistentReportPublishesEachCompletedOrFailedStageBeforeUnwinding()
    {
        WithRunRoot(root =>
        {
            var deployment = Directory.CreateDirectory(Path.Combine(root, "deployment")).FullName;
            string? attached = null;
            WinAppSandboxPrerequisiteReport.CapturePersistent(deployment, path => attached = path, report =>
            {
                report.SetRunId(RunId);
                report.Check("OperatingSystem", stage => stage["Version"] = "10.0.26100.0");
                var path = Directory.GetFiles(root, WinAppSandboxPrerequisiteReport.FileName, SearchOption.AllDirectories).Single();
                Assert.AreEqual(RunId, RunFiles.Read(path)["RunId"]!.GetValue<string>());
                Assert.AreEqual("Passed", RunFiles.Read(path)["Stages"]!["OperatingSystem"]!["Status"]!.GetValue<string>());
                Assert.ThrowsExactly<COMException>(() => report.Check("UserPackageRegistration", stage =>
                {
                    stage["QueryStatus"] = "Failed";
                    Marshal.ThrowExceptionForHR(unchecked((int)0x80073CF6));
                }));
                var package = RunFiles.Read(path)["Stages"]!["UserPackageRegistration"]!;
                Assert.AreEqual("Failed", package["QueryStatus"]!.GetValue<string>());
                Assert.IsNull(package["Present"]);
                Assert.IsNull(package["Count"]);
                Assert.AreEqual("com_query_failed", package["ErrorCodes"]![0]!.GetValue<string>());
                Assert.AreEqual("0x80073CF6", package["ErrorHResults"]![0]!.GetValue<string>());
                Assert.AreEqual("com_query_failed", package["FailureCode"]!.GetValue<string>());
                Assert.AreEqual("0x80073CF6", package["QueryErrorHResult"]!.GetValue<string>());
                Assert.IsFalse(package.ToJsonString().Contains("private", StringComparison.Ordinal));
            });
            Assert.IsNotNull(attached);
        });
    }

    [TestMethod]
    public void CollectorMirrorExistsBeforeTheFixtureRunRootAndSurvivesFailure()
    {
        WithRunRoot(root =>
        {
            var deployment = Path.Combine(root, "deployment");
            var launcher = Directory.CreateDirectory(Path.Combine(root, "ui-owned-invocation")).FullName;
            var fixtureRoot = Path.Combine(launcher, "mwb-" + RunId);
            var mirror = Path.Combine(launcher, WinAppSandboxPrerequisiteReport.CollectorFileName);
            string? attached = null;
            Assert.ThrowsExactly<WinAppSandboxException>(() =>
                WinAppSandboxPrerequisiteReport.CapturePersistent(
                    deployment,
                    path => attached = path,
                    report =>
                    {
                        Assert.IsTrue(File.Exists(mirror));
                        Assert.IsFalse(Directory.Exists(fixtureRoot));
                        report.SetRunId(RunId);
                        report.Check("UserPackageRegistration", stage =>
                        {
                            stage["QueryStatus"] = "Succeeded";
                            stage["Count"] = 0;
                            stage["Present"] = false;
                            stage["Packages"] = new JsonArray();
                            throw new WinAppSandboxException(WinAppSandboxPrerequisite.UserPackageRegistration);
                        });
                    },
                    fixtureRoot));
            Assert.IsNotNull(attached);
            Assert.AreEqual(File.ReadAllText(attached), File.ReadAllText(mirror));
            var package = RunFiles.Read(mirror)["Stages"]!["UserPackageRegistration"]!;
            Assert.AreEqual("trusted_provider_unavailable", package["FailureCode"]!.GetValue<string>());
            Assert.IsNull(package["QueryErrorHResult"], "A successful empty query is not a failed query.");
            Assert.IsFalse(Directory.Exists(fixtureRoot));
        });
    }

    [TestMethod]
    public void UnwritableCollectorMirrorStillAttachesThePrimaryDiagnostic()
    {
        WithRunRoot(root =>
        {
            var launcher = Directory.CreateDirectory(Path.Combine(root, "ui-owned-invocation")).FullName;
            Directory.CreateDirectory(Path.Combine(launcher, WinAppSandboxPrerequisiteReport.CollectorFileName));
            string? attached = null;
            var error = Assert.ThrowsExactly<AggregateException>(() =>
                WinAppSandboxPrerequisiteReport.CapturePersistent(
                    Path.Combine(root, "deployment"),
                    path => attached = path,
                    _ => Assert.Fail("An unavailable collector path must fail before the fixture starts."),
                    Path.Combine(launcher, "mwb-" + RunId)));
            Assert.IsNotNull(attached);
            Assert.IsTrue(File.Exists(attached));
            Assert.IsTrue(error.InnerExceptions.All(item => item is WinAppSandboxException { Code: "prerequisite_report_write_failed" }));
            Assert.IsFalse(error.ToString().Contains(launcher, StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void MissingPackagePersistsConfirmedAbsenceWithoutMaskingTheFailure()
    {
        WithRunRoot(root =>
        {
            var failure = new WinAppSandboxException(WinAppSandboxPrerequisite.UserPackageRegistration);
            var actual = Assert.ThrowsExactly<WinAppSandboxException>(() =>
                WinAppSandboxPrerequisiteReport.Capture(root, RunId, report =>
                {
                    report.Check("OperatingSystem", stage => stage["Version"] = "10.0.26100.0");
                    report.Check("UserPackageRegistration", stage =>
                    {
                        stage["QueryStatus"] = "Succeeded";
                        stage["Present"] = false;
                        stage["Count"] = 0;
                        stage["Packages"] = new JsonArray();
                        throw failure;
                    });
                    Assert.Fail("A missing package must stop prerequisite execution.");
                }));
            Assert.AreSame(failure, actual);
            var stages = ReadStages(root);
            Assert.AreEqual("Passed", stages["OperatingSystem"]!["Status"]!.GetValue<string>());
            Assert.AreEqual("Failed", stages["UserPackageRegistration"]!["Status"]!.GetValue<string>());
            Assert.AreEqual(0, stages["UserPackageRegistration"]!["Count"]!.GetValue<int>());
            Assert.AreEqual("Succeeded", stages["UserPackageRegistration"]!["QueryStatus"]!.GetValue<string>());
            Assert.IsFalse(stages["UserPackageRegistration"]!["Present"]!.GetValue<bool>());
            Assert.AreEqual(0, stages["UserPackageRegistration"]!["Packages"]!.AsArray().Count);
            foreach (var name in StageNames.Skip(2))
            {
                Assert.AreEqual("NotChecked", stages[name]!["Status"]!.GetValue<string>());
            }

            CollectionAssert.AreEqual(
                new[] { Path.Combine(root, "winapp-prerequisites.json") },
                RunFiles.EvidenceFiles(root).ToArray(),
                "Fixture cleanup must attach the public JSON, but not its publication marker.");
        });
    }

    [TestMethod]
    public void FailedPackageQueryDoesNotClaimAbsenceOrPublishRawErrors()
    {
        WithRunRoot(root =>
        {
            var failure = new UnauthorizedAccessException(@"private-token C:\private-control\bootstrap.json");
            var actual = Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
                WinAppSandboxPrerequisiteReport.Capture(root, RunId, report =>
                    report.Check("UserPackageRegistration", _ => throw failure)));
            Assert.AreSame(failure, actual);
            var stages = ReadStages(root);
            Assert.AreEqual("Failed", stages["UserPackageRegistration"]!["Status"]!.GetValue<string>());
            Assert.IsNull(stages["UserPackageRegistration"]!["Count"]);
            Assert.IsNull(stages["UserPackageRegistration"]!["Packages"]);
            Assert.AreEqual("NotChecked", stages["UserExecutionAlias"]!["Status"]!.GetValue<string>());
            Assert.IsFalse(stages.ToJsonString().Contains("private-token", StringComparison.Ordinal));
            Assert.IsFalse(stages.ToJsonString().Contains("bootstrap", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void InterruptedAliasCheckRetainsOnlyTheObservationsAlreadyMade()
    {
        WithRunRoot(root =>
        {
            Assert.ThrowsExactly<IOException>(() =>
                WinAppSandboxPrerequisiteReport.Capture(root, RunId, report =>
                    report.Check("UserExecutionAlias", stage =>
                    {
                        stage["Exists"] = true;
                        throw new IOException("private attributes failure");
                    })));
            var stages = ReadStages(root);
            Assert.AreEqual("Failed", stages["UserExecutionAlias"]!["Status"]!.GetValue<string>());
            Assert.IsTrue(stages["UserExecutionAlias"]!["Exists"]!.GetValue<bool>());
            Assert.IsNull(stages["UserExecutionAlias"]!["IsReparsePoint"]);
            Assert.AreEqual("NotChecked", stages["PackageExecutable"]!["Status"]!.GetValue<string>());
        });
    }

    [TestMethod]
    public void SuccessfulChecksKeepTheirResultsWithoutRepeatingTheOperations()
    {
        WithRunRoot(root =>
        {
            var calls = 0;
            WinAppSandboxPrerequisiteReport.Capture(root, RunId, report =>
            {
                foreach (var name in StageNames)
                {
                    var result = report.Check(name, stage =>
                    {
                        calls++;
                        if (name == "UserExecutionAlias")
                        {
                            stage["Exists"] = true;
                            stage["IsReparsePoint"] = true;
                        }
                        else if (name == "PackageExecutable")
                        {
                            stage["InstalledLocation"] = @"C:\Program Files\WindowsApps\MicrosoftWindows.WindowsSandbox";
                            stage["Exists"] = true;
                        }
                        else if (name == "ProviderVersion")
                        {
                            stage["VersionLine"] = WinAppSandboxPrerequisiteReport.SanitizeVersionLine(
                                "private banner\nWindows Sandbox CLI version: 0.4.3.0\r\nprivate trailing output");
                        }

                        return calls;
                    });
                    Assert.AreEqual(calls, result);
                }
            });
            Assert.AreEqual(StageNames.Length, calls);
            var stages = ReadStages(root);
            Assert.IsTrue(stages.All(stage => stage.Value!["Status"]!.GetValue<string>() == "Passed"));
            Assert.IsTrue(stages["UserExecutionAlias"]!["Exists"]!.GetValue<bool>());
            Assert.IsTrue(stages["UserExecutionAlias"]!["IsReparsePoint"]!.GetValue<bool>());
            Assert.IsTrue(stages["PackageExecutable"]!["Exists"]!.GetValue<bool>());
            Assert.AreEqual(
                @"C:\Program Files\WindowsApps\MicrosoftWindows.WindowsSandbox",
                stages["PackageExecutable"]!["InstalledLocation"]!.GetValue<string>());
            Assert.AreEqual("wsb 0.4.3.0", stages["ProviderVersion"]!["VersionLine"]!.GetValue<string>());
            Assert.IsFalse(stages.ToJsonString().Contains("private", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void FailedVersionProbePreservesAllCausesAndWithholdsVersionOutput()
    {
        WithRunRoot(root =>
        {
            var failure = new AggregateException(
                new WinAppSandboxException(WinAppSandboxPrerequisite.ProviderVersion),
                new WinAppSandboxException("command_timeout"));
            var actual = Assert.ThrowsExactly<AggregateException>(() =>
                WinAppSandboxPrerequisiteReport.Capture(root, RunId, report =>
                    report.Check("ProviderVersion", _ => throw failure)));
            Assert.AreSame(failure, actual);
            var stage = ReadStages(root)["ProviderVersion"]!;
            Assert.AreEqual("Failed", stage["Status"]!.GetValue<string>());
            Assert.IsNull(stage["VersionLine"]);
            Assert.IsTrue(stage["TimedOut"]!.GetValue<bool>());
            CollectionAssert.Contains(stage["ErrorCodes"]!.AsArray().Select(code => code!.GetValue<string>()).ToArray(), "command_timeout");
        });
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(7)]
    public void VersionProbeRecordsActualExitAndOnlyAllowlistedOutputBeforeFailure(int exitCode)
    {
        WithRunRoot(root =>
        {
            void Execute() => WinAppSandboxPrerequisiteReport.Capture(root, RunId, report =>
                report.CheckProviderVersion(TimeSpan.FromSeconds(15), observe =>
                {
                    var result = new WinAppSandboxCommand.Result(exitCode, "private banner\r\nwsb 0.8.107.0\r\nprivate token", "private error");
                    observe(result);
                    return result.RequireSuccess();
                }));
            if (exitCode == 0)
            {
                Execute();
            }
            else
            {
                Assert.ThrowsExactly<AggregateException>(Execute);
            }

            var stage = ReadStages(root)["ProviderVersion"]!;
            Assert.AreEqual(exitCode, stage["ExitCode"]!.GetValue<int>());
            Assert.AreEqual(15D, stage["TimeoutSeconds"]!.GetValue<double>());
            Assert.IsFalse(stage["TimedOut"]!.GetValue<bool>());
            Assert.AreEqual("wsb 0.8.107.0", stage["VersionLine"]!.GetValue<string>());
            Assert.IsFalse(stage.ToJsonString().Contains("private", StringComparison.Ordinal));
            Assert.AreEqual(exitCode == 0 ? "Passed" : "Failed", stage["Status"]!.GetValue<string>());
            if (exitCode != 0)
            {
                CollectionAssert.Contains(stage["ErrorCodes"]!.AsArray().Select(code => code!.GetValue<string>()).ToArray(), "command_failed");
            }
        });
    }

    [TestMethod]
    public void TimedOutProviderDoesNotInventAnExitStatusOrVersion()
    {
        WithRunRoot(root =>
        {
            Assert.ThrowsExactly<AggregateException>(() =>
                WinAppSandboxPrerequisiteReport.Capture(root, RunId, report =>
                    report.CheckProviderVersion(TimeSpan.FromSeconds(15), _ =>
                        throw new WinAppSandboxException("command_timeout"))));
            var stage = ReadStages(root)["ProviderVersion"]!;
            Assert.IsNull(stage["ExitCode"]);
            Assert.IsNull(stage["VersionLine"]);
            Assert.IsTrue(stage["TimedOut"]!.GetValue<bool>());
            Assert.AreEqual(15D, stage["TimeoutSeconds"]!.GetValue<double>());
        });
    }

    [TestMethod]
    public void UnknownErrorCodesAreNeverPublishedAsUserControlledText()
    {
        WithRunRoot(root =>
        {
            Assert.ThrowsExactly<WinAppSandboxException>(() =>
                WinAppSandboxPrerequisiteReport.Capture(root, RunId, report =>
                    report.Check("CliSchema", _ => throw new WinAppSandboxException("private-token"))));
            var stage = ReadStages(root)["CliSchema"]!;
            Assert.AreEqual("prerequisite_check_failed", stage["ErrorCodes"]![0]!.GetValue<string>());
            Assert.IsFalse(stage.ToJsonString().Contains("private-token", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void EvidenceWriteFailureIsSanitizedAndPreservesTheOriginalPrerequisiteFailure()
    {
        WithRunRoot(root =>
        {
            Directory.CreateDirectory(Path.Combine(root, WinAppSandboxPrerequisiteReport.FileName));
            var failure = new WinAppSandboxException(WinAppSandboxPrerequisite.UserPackageRegistration);
            var aggregate = Assert.ThrowsExactly<AggregateException>(() =>
                WinAppSandboxPrerequisiteReport.Capture(root, RunId, _ => throw failure));
            Assert.AreEqual(2, aggregate.InnerExceptions.Count);
            Assert.AreSame(failure, aggregate.InnerExceptions[0]);
            Assert.AreEqual("prerequisite_report_write_failed", ((WinAppSandboxException)aggregate.InnerExceptions[1]).Code);
            Assert.IsFalse(aggregate.ToString().Contains(root, StringComparison.Ordinal));

            var writeFailure = Assert.ThrowsExactly<WinAppSandboxException>(() =>
                WinAppSandboxPrerequisiteReport.Capture(root, RunId, _ => { }));
            Assert.AreEqual("prerequisite_report_write_failed", writeFailure.Code);
        });
    }

    [DataTestMethod]
    [DataRow("0.4.3", "wsb 0.4.3")]
    [DataRow("0.4.3.0", "wsb 0.4.3.0")]
    [DataRow("wsb.exe version: 0.4.3.0\r\n", "wsb 0.4.3.0")]
    [DataRow("Windows Sandbox version: 0.4.3", "wsb 0.4.3")]
    [DataRow("Windows Sandbox CLI version 0.4.3.0", "wsb 0.4.3.0")]
    [DataRow("  Version: v1.20.300  ", "wsb 1.20.300")]
    [DataRow("private banner\r\nwsb 0.4.3.0\r\nprivate token", "wsb 0.4.3.0")]
    [DataRow("", null)]
    [DataRow("private banner 0.4.3.0", null)]
    [DataRow("wsb 0.4.3.0 private-token", null)]
    [DataRow("wsb 0.4.3.0.1", null)]
    [DataRow("wsb 0.4", null)]
    [DataRow("wsb 0.4.3-private-token", null)]
    [DataRow("wsb 0.4.3.0\\bootstrap.json", null)]
    [DataRow("\u001b[32mwsb 0.4.3.0\u001b[0m", null)]
    public void VersionEvidenceAcceptsOnlyCompleteThreeOrFourComponentVersionLines(string output, string? expected)
    {
        Assert.AreEqual(expected, WinAppSandboxPrerequisiteReport.SanitizeVersionLine(output));
    }

    private static JsonObject ReadStages(string root) =>
        RunFiles.Read(Path.Combine(root, WinAppSandboxPrerequisiteReport.FileName))["Stages"]!.AsObject();

    private static void WithRunRoot(Action<string> test)
    {
        var root = Path.Combine(Environment.CurrentDirectory, "TestResults", "mwb-prerequisites-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            test(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
