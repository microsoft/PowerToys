// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;
using PowerScripts.UI.ViewModels;

namespace ViewModelTests;

[TestClass]
public sealed class PowerScriptRunViewModelTests
{
    private static readonly string[] TextFileExtensions = { ".txt" };
    private static readonly string[] ExpectedInputFiles = { "C:\\one.txt", "C:\\two.txt" };

    [TestMethod]
    public void RequiredValuePreventsRunUntilProvided()
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(Script(new PowerScriptParameter("topic", "string", true, null, null, null, Array.Empty<string>(), null, null)));

        Assert.IsFalse(model.CanRun);
        Assert.IsTrue(model.HasValidationError);
        model.Parameters[0].TextValue = "PowerToys";

        Assert.IsTrue(model.CanRun);
        Assert.IsTrue(model.TryCreateInput(out var input, out _));
        Assert.AreEqual("PowerToys", input.Parameters["topic"].GetString());
    }

    [TestMethod]
    public void OptionalEmptyFalseAndZeroRemainExplicitWhenSelected()
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(Script(
            new("label", "string", false, null, null, null, Array.Empty<string>(), null, null),
            new("verbose", "bool", false, null, null, null, Array.Empty<string>(), null, null),
            new("count", "int", false, null, null, null, Array.Empty<string>(), null, null)));
        Assert.IsTrue(model.TryCreateInput(out var omitted, out _));
        Assert.IsNull(omitted.Parameters);
        foreach (var parameter in model.Parameters)
        {
            parameter.IsProvided = true;
        }

        model.Parameters[2].TextValue = "0";
        Assert.IsTrue(model.TryCreateInput(out var supplied, out _));
        Assert.AreEqual(string.Empty, supplied.Parameters["label"].GetString());
        Assert.IsFalse(supplied.Parameters["verbose"].GetBoolean());
        Assert.AreEqual(0, supplied.Parameters["count"].GetInt32());
    }

    [TestMethod]
    public void RefreshKeepsDraftAndRequiresNewRequiredValue()
    {
        var model = new PowerScriptRunViewModel();
        var topic = new PowerScriptParameter("topic", "string", true, "Topic", null, null, Array.Empty<string>(), null, null);
        model.FinishLoading(Script(topic));
        model.Parameters[0].TextValue = "Saved draft";

        model.FinishLoading(Script(topic, new("count", "int", true, null, null, null, Array.Empty<string>(), 1, 5)));

        Assert.AreEqual("Saved draft", model.Parameters[0].TextValue);
        Assert.IsFalse(model.CanRun);
        model.Parameters[1].TextValue = "2";
        Assert.IsTrue(model.CanRun);
    }

    [TestMethod]
    public void RequiredParameterWithDefaultCanBeOmitted()
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(Script(new PowerScriptParameter("count", "int", true, null, null, "2", Array.Empty<string>(), 1, 5)));
        Assert.IsTrue(model.Parameters[0].CanOmit);
        model.Parameters[0].IsProvided = false;

        Assert.IsTrue(model.CanRun);
        Assert.IsTrue(model.TryCreateInput(out var input, out _));
        Assert.IsNull(input.Parameters);
    }

    [TestMethod]
    public void NewRunClearsOldOutputAndRetainsFailureDiagnostics()
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(Script());
        model.BeginRun();
        model.Complete(new PowerScriptTransformResult { ExitCode = 0, Text = "old output", StdErr = "old diagnostics" });
        Assert.IsTrue(model.HasOutput);

        model.BeginRun();
        Assert.IsFalse(model.HasOutput);
        Assert.IsFalse(model.HasDiagnostics);
        Assert.AreEqual(string.Empty, model.ExitCode);
        Assert.IsFalse(model.CanRun);
        Assert.IsFalse(model.CanEdit);
        model.Complete(new PowerScriptTransformResult { ExitCode = 7, StdErr = "failure details" });

        Assert.AreEqual("failure details", model.Diagnostics);
        Assert.IsFalse(model.IsRunning);
        Assert.AreEqual(PowerScriptsViewModel.GetResourceString("PowerScripts_RunFailed"), model.Status);
    }

    [TestMethod]
    public void CancellationDoesNotClaimExecutionWasReversedOrStopped()
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(Script());
        model.BeginRun();
        model.RequestCancellation();
        Assert.IsTrue(model.IsRunning);

        model.Cancelled(timedOut: false);

        Assert.AreEqual(PowerScriptsViewModel.GetResourceString("PowerScripts_RunCancelled"), model.Status);
        Assert.AreEqual(PowerScriptsViewModel.GetResourceString("PowerScripts_RunExecutionUnknown"), model.FailureMessage);
        Assert.IsFalse(model.IsRunning);
        Assert.IsFalse(model.HasOutput);
    }

    [TestMethod]
    public void EditingInputClearsThePreviousResult()
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(Script(new PowerScriptParameter("topic", "string", true, null, null, "first", Array.Empty<string>(), null, null)));
        model.BeginRun();
        model.Complete(new PowerScriptTransformResult { ExitCode = 0, Text = "first result" });

        model.Parameters[0].TextValue = "second";

        Assert.IsFalse(model.HasOutput);
        Assert.AreEqual(string.Empty, model.ExitCode);
        Assert.AreEqual(PowerScriptsViewModel.GetResourceString("PowerScripts_RunReady"), model.Status);
    }

    [TestMethod]
    public void NativeOperationUsesInputContractRatherThanLanguage()
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(new PowerScriptInfo { Id = "operation", Runtime = "PowerToysOperation", Input = PowerScriptIO.None, Output = PowerScriptIO.Text });

        Assert.IsTrue(model.IsSupported);
        Assert.IsTrue(model.HasNoParameters);
        Assert.IsTrue(model.CanRun);
    }

    [DataTestMethod]
    [DataRow(PowerScriptIO.Image, PowerScriptIO.Text)]
    [DataRow(PowerScriptIO.Text, PowerScriptIO.Audio)]
    public void UnsupportedMediaNeedsCompatibleConsumer(PowerScriptIO input, PowerScriptIO output)
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(new PowerScriptInfo { Id = "media", Input = input, Output = output });

        Assert.IsFalse(model.IsSupported);
        Assert.IsFalse(model.CanRun);
        Assert.IsTrue(model.HasUnsupportedReason);
    }

    [TestMethod]
    public void FileInputRespectsCountAndExtensions()
    {
        var model = new PowerScriptRunViewModel();
        model.FinishLoading(new PowerScriptInfo
        {
            Id = "file-input",
            Input = PowerScriptIO.Files,
            Output = PowerScriptIO.Text,
            InputExtensions = TextFileExtensions,
            InputMinFiles = 1,
            InputMaxFiles = 2,
        });
        Assert.IsFalse(model.CanRun);
        model.PrimaryFilesText = "C:\\one.txt\r\nC:\\two.txt";
        Assert.IsTrue(model.CanRun);
        Assert.IsTrue(model.TryCreateInput(out var input, out _));
        CollectionAssert.AreEqual(ExpectedInputFiles, input.FilePaths.ToArray());

        model.PrimaryFilesText = "C:\\one.png";
        Assert.IsFalse(model.CanRun);
    }

    private static PowerScriptInfo Script(params PowerScriptParameter[] parameters) => new()
    {
        Id = "example",
        Input = PowerScriptIO.None,
        Output = PowerScriptIO.Text,
        Parameters = parameters,
    };
}
