// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RobocopyUI.Helpers;
using RobocopyUI.Models;
using RobocopyUI.Services.AI;

namespace RobocopyUI.UnitTests;

[TestClass]
public class SimpleCopyTaskTests
{
    [TestMethod]
    public void Expand_CopyFilesAndFolders_EmitsE()
    {
        var options = SimpleCopyTask.Expand(SimpleCopyTaskKind.CopyFilesAndFolders, new SimpleCopyOptions());

        Assert.AreEqual("/E", SingleName(options));
        Assert.AreEqual(string.Empty, options[0].Value);
    }

    [TestMethod]
    public void Expand_CopySkipEmptyFolders_EmitsS()
    {
        var options = SimpleCopyTask.Expand(SimpleCopyTaskKind.CopySkipEmptyFolders, new SimpleCopyOptions());

        Assert.AreEqual("/S", SingleName(options));
    }

    [TestMethod]
    public void Expand_CopyThisFolderOnly_EmitsNoRecursionSwitch()
    {
        var options = SimpleCopyTask.Expand(SimpleCopyTaskKind.CopyThisFolderOnly, new SimpleCopyOptions());

        Assert.AreEqual(0, options.Count);
    }

    [TestMethod]
    public void Expand_Mirror_EmitsMir()
    {
        var options = SimpleCopyTask.Expand(SimpleCopyTaskKind.Mirror, new SimpleCopyOptions());

        Assert.AreEqual("/MIR", SingleName(options));
    }

    [TestMethod]
    public void Expand_KeepAllPropertiesAndPermissions_PrunesToCopyAll()
    {
        var options = SimpleCopyTask.Expand(
            SimpleCopyTaskKind.CopyFilesAndFolders,
            new SimpleCopyOptions { KeepPermissions = true, KeepAllProperties = true });

        Assert.IsTrue(HasNames(options, "/E", "/COPYALL"));
        Assert.IsFalse(options.Any(option => option.Name == "/SEC"));
    }

    [TestMethod]
    public void Expand_OptionalToggles_EmitExpectedSwitches()
    {
        var options = SimpleCopyTask.Expand(
            SimpleCopyTaskKind.CopyFilesAndFolders,
            new SimpleCopyOptions
            {
                KeepPermissions = true,
                SkipNewerAtDestination = true,
                ExcludeFileTypes = "*.tmp *.log",
                ExcludeFolders = "Temp",
                CopyFaster = true,
                ThreadCount = 16,
                RetryFailedFiles = true,
                RetryCount = 2,
                RetryWaitSeconds = 7,
                PreviewOnly = true,
                WriteLog = true,
                LogPath = @"C:\logs\copy.log",
            });

        Assert.AreEqual(string.Empty, Value(options, "/SEC"));
        Assert.AreEqual(string.Empty, Value(options, "/XO"));
        Assert.AreEqual("*.tmp *.log", Value(options, "/XF"));
        Assert.AreEqual("Temp", Value(options, "/XD"));
        Assert.AreEqual("16", Value(options, "/MT"));
        Assert.AreEqual("2", Value(options, "/R"));
        Assert.AreEqual("7", Value(options, "/W"));
        Assert.AreEqual(string.Empty, Value(options, "/L"));
        Assert.AreEqual(@"C:\logs\copy.log", Value(options, "/LOG"));
    }

    [TestMethod]
    public void Merge_PreservesAdvancedExtras()
    {
        var existing = new List<RobocopyPlanOption>
        {
            new("/E", string.Empty),
            new("/Z", string.Empty),
            new("/B", string.Empty),
        };

        var merged = SimpleCopyTask.Merge(existing, SimpleCopyTaskKind.Mirror, new SimpleCopyOptions());

        Assert.IsTrue(HasNames(merged, "/MIR", "/Z", "/B"));
        Assert.IsFalse(merged.Any(option => option.Name == "/E"));
    }

    [TestMethod]
    public void Infer_ReadsJobAndAdditionalOptions()
    {
        var snapshot = SimpleCopyTask.Infer(
        [
            new("/MIR", string.Empty),
            new("/MT", "32"),
            new("/Z", string.Empty),
        ]);

        Assert.AreEqual(SimpleCopyTaskKind.Mirror, snapshot.Kind);
        Assert.IsTrue(snapshot.Options.CopyFaster);
        Assert.AreEqual(32, snapshot.Options.ThreadCount);
        Assert.IsTrue(snapshot.HasAdditionalOptions);
    }

    [TestMethod]
    public void Infer_EmptyOptions_IsThisFolderOnlyWithoutExtras()
    {
        var snapshot = SimpleCopyTask.Infer([]);

        Assert.AreEqual(SimpleCopyTaskKind.CopyThisFolderOnly, snapshot.Kind);
        Assert.IsFalse(snapshot.HasAdditionalOptions);
    }

    [TestMethod]
    public void Prune_Mir_DropsEAndSAndPurge()
    {
        var options = new List<RobocopyPlanOption>
        {
            new("/MIR", string.Empty),
            new("/E", string.Empty),
            new("/S", string.Empty),
            new("/PURGE", string.Empty),
        };

        RobocopyCommand.Prune(options);

        Assert.AreEqual("/MIR", SingleName(options));
    }

    [TestMethod]
    public void Prune_S_DropsE()
    {
        var options = new List<RobocopyPlanOption>
        {
            new("/E", string.Empty),
            new("/S", string.Empty),
        };

        RobocopyCommand.Prune(options);

        Assert.AreEqual("/S", SingleName(options));
    }

    [TestMethod]
    public void Prune_CopyAll_DropsCopyAndSec()
    {
        var options = new List<RobocopyPlanOption>
        {
            new("/COPYALL", string.Empty),
            new("/COPY", "DAT"),
            new("/SEC", string.Empty),
        };

        RobocopyCommand.Prune(options);

        Assert.AreEqual("/COPYALL", SingleName(options));
    }

    [TestMethod]
    public void Prune_Mir_DropsX()
    {
        var options = new List<RobocopyPlanOption>
        {
            new("/MIR", string.Empty),
            new("/X", string.Empty),
        };

        RobocopyCommand.Prune(options);

        Assert.IsFalse(options.Any(option => option.Name == "/X"));
    }

    [TestMethod]
    public void Render_FormatsXfAsSpaceSeparatedSwitch()
    {
        var command = RobocopyCommand.Render(
            @"C:\Src",
            @"D:\Dst",
            [new RobocopyPlanOption("/E", string.Empty), new RobocopyPlanOption("/XF", "*.tmp *.log")]);

        Assert.AreEqual(@"robocopy.exe C:\Src D:\Dst /E /XF *.tmp *.log", command);
    }

    [TestMethod]
    public void GetDestructiveWarnings_IncludesMirrorWarning()
    {
        var warnings = RobocopyCommand.GetDestructiveWarnings([new RobocopyPlanOption("/MIR", string.Empty)]);

        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "/MIR");
    }

    [TestMethod]
    public void Job_SetOptionMir_RemovesImpliedE()
    {
        var job = new RobocopyJob { Source = @"C:\A", Destination = @"D:\B" };
        job.SetOption("/E", string.Empty);
        job.SetOption("/MIR", string.Empty);

        Assert.AreEqual("/MIR", SingleName([.. job.Options]));
        Assert.AreEqual(@"robocopy.exe C:\A D:\B /MIR", job.RenderCommandLine());
    }

    [TestMethod]
    public void Job_ApplySimple_KeepsUnownedAdvancedSwitch()
    {
        var job = new RobocopyJob();
        job.SetOption("/Z", string.Empty);
        job.ApplySimple(SimpleCopyTaskKind.CopyFilesAndFolders, new SimpleCopyOptions { PreviewOnly = true });

        Assert.IsTrue(HasNames(job.Options, "/E", "/L", "/Z"));
    }

    private static string SingleName(List<RobocopyPlanOption> options)
    {
        Assert.AreEqual(1, options.Count);
        return options[0].Name;
    }

    private static string Value(IEnumerable<RobocopyPlanOption> options, string name) =>
        options.First(option => option.Name == name).Value;

    private static bool HasNames(IEnumerable<RobocopyPlanOption> options, params string[] names)
    {
        var actual = options.Select(option => option.Name).ToHashSet();
        return actual.SetEquals(names);
    }
}
