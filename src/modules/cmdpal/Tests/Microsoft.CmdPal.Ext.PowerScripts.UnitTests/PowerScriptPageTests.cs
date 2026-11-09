// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class PowerScriptPageTests
{
    [TestMethod]
    public void RepeatedContentRequestsDoNotExecuteTheScript()
    {
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptPage(service, TestScripts.Create());

        var first = page.GetContent();
        var second = page.GetContent();

        Assert.HasCount(1, first);
        Assert.AreSame(page.ParameterForm, first[0]);
        Assert.AreSame(first[0], second[0]);
        Assert.AreEqual(0, service.RunCalls);
        Assert.AreEqual(0, service.DiscoverCalls);
    }

    [TestMethod]
    [DataRow("{", "{\"action\":\"run\"}")]
    [DataRow("[]", "{\"action\":\"run\"}")]
    [DataRow("null", "{\"action\":\"run\"}")]
    [DataRow("\"text\"", "{\"action\":\"run\"}")]
    [DataRow("{\"parameter_0\":{}}", "{\"action\":\"run\"}")]
    [DataRow("{\"parameter_0\":[]}", "{\"action\":\"run\"}")]
    [DataRow("{\"parameter_0\":null}", "{\"action\":\"run\"}")]
    [DataRow("{}", "{")]
    [DataRow("{}", "[]")]
    [DataRow("{}", "{\"action\":1}")]
    [DataRow("{}", "{\"action\":\"unknown\"}")]
    public void MalformedSubmissionsKeepTheFormOpenWithoutRunning(string inputs, string action)
    {
        var script = TestScripts.Create(TestScripts.Parameter("value", required: true));
        var service = new FakePowerScriptsService();
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);

        var result = page.ParameterForm.SubmitForm(inputs, action);

        Assert.AreEqual(CommandResultKind.KeepOpen, result.Kind);
        Assert.AreEqual(0, service.RunCalls);
        Assert.IsNull(session.Completion);
        using var data = JsonDocument.Parse(page.ParameterForm.DataJson);
        Assert.IsTrue(data.RootElement.TryGetProperty("formError", out var error));
        Assert.IsFalse(string.IsNullOrEmpty(error.GetString()));
    }

    [TestMethod]
    public void InvalidIntegerRemainsVisibleWithItsValidationError()
    {
        var script = TestScripts.Create(TestScripts.Parameter("count", "int", required: true));
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptPage(service, script);

        page.ParameterForm.SubmitForm("""{"parameter_0":"not a number"}""", """{"action":"run"}""");

        Assert.AreEqual(0, service.RunCalls);
        using var data = JsonDocument.Parse(page.ParameterForm.DataJson);
        var field = data.RootElement.GetProperty("fields")[0];
        Assert.AreEqual("not a number", field.GetProperty("value").GetString());
        Assert.IsFalse(string.IsNullOrEmpty(field.GetProperty("error").GetString()));
        using var template = JsonDocument.Parse(page.ParameterForm.TemplateJson);
        var input = template.RootElement.GetProperty("body").EnumerateArray()
            .Single(element => element.TryGetProperty("id", out var id) && id.GetString() == "parameter_0");
        Assert.AreEqual("Input.Text", input.GetProperty("type").GetString());
    }

    [TestMethod]
    public void MetadataOnlyRefreshPreservesEnteredValuesAndValidationErrors()
    {
        var parameter = TestScripts.Parameter("count", "int", required: true, defaultValue: "2");
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptPage(service, TestScripts.Create(parameter));
        page.ParameterForm.SubmitForm("""{"parameter_0":"invalid integer"}""", """{"action":"run"}""");
        using var previousData = JsonDocument.Parse(page.ParameterForm.DataJson);
        var previousError = previousData.RootElement.GetProperty("fields")[0].GetProperty("error").GetString();

        page.UpdateScript(TestScripts.Create(parameter with { Label = "Updated label", Description = "Updated help" }));

        using var refreshedData = JsonDocument.Parse(page.ParameterForm.DataJson);
        var field = refreshedData.RootElement.GetProperty("fields")[0];
        Assert.AreEqual("invalid integer", field.GetProperty("value").GetString());
        Assert.AreEqual(previousError, field.GetProperty("error").GetString());
        Assert.AreEqual("Updated help", field.GetProperty("description").GetString());
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    public void ChangedParameterContractResetsToNewDefaultsAndExplainsTheReset()
    {
        var parameter = TestScripts.Parameter("count", "int", required: true, defaultValue: "2");
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptPage(service, TestScripts.Create(parameter));
        page.ParameterForm.SubmitForm("""{"parameter_0":"invalid integer"}""", """{"action":"run"}""");

        page.UpdateScript(TestScripts.Create(parameter with { Default = "4", Min = 3 }));

        using var data = JsonDocument.Parse(page.ParameterForm.DataJson);
        var field = data.RootElement.GetProperty("fields")[0];
        Assert.AreEqual("4", field.GetProperty("value").GetString());
        Assert.AreEqual(string.Empty, field.GetProperty("error").GetString());
        Assert.AreEqual(Resources.ParametersChanged, data.RootElement.GetProperty("parametersChanged").GetString());
        using var template = JsonDocument.Parse(page.ParameterForm.TemplateJson);
        Assert.IsTrue(template.RootElement.GetProperty("body").EnumerateArray()
            .Any(element => element.TryGetProperty("text", out var text) && text.GetString() == "${parametersChanged}"));
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ValidTypedValuesAreSubmittedOnceWhileExecutionIsPending()
    {
        var result = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService { RunHandler = _ => result.Task };
        var script = TestScripts.Create(
            TestScripts.Parameter("enabled", "bool"),
            TestScripts.Parameter("count", "int"),
            TestScripts.Parameter("text"));
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);
        const string inputs = """{"include_0":true,"parameter_0":false,"include_1":"true","parameter_1":0,"include_2":"true","parameter_2":""}""";

        Assert.AreEqual(CommandResultKind.KeepOpen, page.ParameterForm.SubmitForm(inputs, """{"action":"run"}""").Kind);
        page.ParameterForm.SubmitForm(inputs, """{"action":"run"}""");
        page.GetContent();
        await service.RunStarted.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(1, service.RunCalls);
        Assert.AreEqual(JsonValueKind.False, service.LastParameters!["enabled"].ValueKind);
        Assert.AreEqual(0, service.LastParameters!["count"].GetInt32());
        Assert.AreEqual(string.Empty, service.LastParameters!["text"].GetString());

        result.SetResult(new PowerScriptTransformResult());
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task StopActionDoesNotRequireResubmittingParameterValues()
    {
        var service = new FakePowerScriptsService
        {
            RunHandler = async cancellationToken =>
            {
                await Task.Delay(-1, cancellationToken);
                return new PowerScriptTransformResult();
            },
        };
        var script = TestScripts.Create(TestScripts.Parameter("value", required: true));
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);
        page.ParameterForm.SubmitForm("""{"parameter_0":"submitted"}""", """{"action":"run"}""");
        await service.RunStarted.WaitAsync(TimeSpan.FromSeconds(4));

        var commandResult = page.ParameterForm.SubmitForm(string.Empty, """{"action":"stop"}""");
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(CommandResultKind.KeepOpen, commandResult.Kind);
        Assert.AreEqual(PowerScriptRunState.Stopped, session.State);
        Assert.AreEqual(1, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task EmptyOutputAndDiagnosticsRemainSeparateAndCopyable()
    {
        var service = new FakePowerScriptsService
        {
            RunHandler = _ => Task.FromResult(new PowerScriptTransformResult { Text = string.Empty, StdErr = "Diagnostic details." }),
        };
        var script = TestScripts.Create();
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);

        page.ParameterForm.SubmitForm("{}", """{"action":"run"}""");
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
        Assert.HasCount(3, page.GetContent());
        Assert.IsInstanceOfType<PlainTextContent>(page.GetContent()[0]);
        Assert.AreSame(page.ParameterForm, page.GetContent()[1]);
        var copyCommands = page.Commands.OfType<ICommandContextItem>().Select(item => item.Command).OfType<CopyTextCommand>().ToArray();
        Assert.HasCount(2, copyCommands);
        Assert.IsTrue(copyCommands.Any(command => command.Text == string.Empty));
        Assert.IsTrue(copyCommands.Any(command => command.Text == "Diagnostic details."));
    }

    [TestMethod]
    public void OptionalParameterTogglesHaveDistinctAccessibleLabels()
    {
        var script = TestScripts.Create(TestScripts.Parameter("first"), TestScripts.Parameter("second"));
        using var page = new PowerScriptPage(new FakePowerScriptsService(), script);
        using var template = JsonDocument.Parse(page.ParameterForm.TemplateJson);
        var toggles = template.RootElement.GetProperty("body").EnumerateArray()
            .Where(element => element.TryGetProperty("type", out var type) && type.GetString() == "Input.Toggle").ToArray();
        using var data = JsonDocument.Parse(page.ParameterForm.DataJson);

        Assert.HasCount(2, toggles);
        Assert.AreEqual("${fields[0].includeLabel}", toggles[0].GetProperty("label").GetString());
        Assert.AreEqual("${fields[1].includeLabel}", toggles[1].GetProperty("label").GetString());
        Assert.AreEqual(string.Empty, toggles[0].GetProperty("title").GetString());
        Assert.AreEqual(string.Empty, toggles[1].GetProperty("title").GetString());
        var fields = data.RootElement.GetProperty("fields");
        var firstLabel = fields[0].GetProperty("includeLabel").GetString()!;
        var secondLabel = fields[1].GetProperty("includeLabel").GetString()!;
        StringAssert.Contains(firstLabel, "first");
        StringAssert.Contains(secondLabel, "second");
        Assert.AreNotEqual(firstLabel, secondLabel);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task LargeOutputHasABoundedPreviewAndPreservesTheCompleteCopyValue()
    {
        var output = new string('a', 16383) + "😀" + new string('b', 20000);
        var service = new FakePowerScriptsService
        {
            RunHandler = _ => Task.FromResult(new PowerScriptTransformResult { Text = output }),
        };
        var script = TestScripts.Create();
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);

        page.ParameterForm.SubmitForm("{}", """{"action":"run"}""");
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.IsInstanceOfType<PlainTextContent>(page.GetContent()[0]);
        Assert.AreSame(page.ParameterForm, page.GetContent()[1]);
        var preview = page.GetContent().OfType<PlainTextContent>().Single().Text!;
        Assert.IsTrue(preview.Length < output.Length);
        Assert.IsFalse(preview.Any(char.IsSurrogate), "Truncation must not leave the first half of the boundary emoji in the preview.");
        var copy = page.Commands.OfType<ICommandContextItem>().Select(item => item.Command).OfType<CopyTextCommand>().Single();
        Assert.AreEqual(output, copy.Text);
    }

    [TestMethod]
    public void ScriptMetadataIsDataBoundRatherThanEmbeddedInTheCardTemplate()
    {
        const string metadata = "${unexpected.expression} \"quoted\" 中文";
        var script = new PowerScriptInfo
        {
            Id = "metadata.script",
            Name = metadata,
            Description = metadata,
            Parameters = [new PowerScriptParameter("value", "string", false, metadata, metadata, metadata, [], null, null)],
        };
        using var page = new PowerScriptPage(new FakePowerScriptsService(), script);

        using var data = JsonDocument.Parse(page.ParameterForm.DataJson);
        Assert.AreEqual(metadata, data.RootElement.GetProperty("description").GetString());
        Assert.AreEqual(metadata, data.RootElement.GetProperty("fields")[0].GetProperty("value").GetString());
        Assert.IsFalse(page.ParameterForm.TemplateJson.Contains("unexpected.expression", StringComparison.Ordinal));
    }

    // Exercise both structurally invalid input objects and truncated JSON with a reproducible bounded corpus.
    [TestMethod]
    public void BoundedRandomNestedAndTruncatedPayloadsCannotStartExecution()
    {
        var random = new Random(0x71F0);
        var service = new FakePowerScriptsService();
        var script = TestScripts.Create(TestScripts.Parameter("value", required: true));
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);

        for (var iteration = 0; iteration < 128; iteration++)
        {
            var value = random.Next(2) == 0 ? "null" : "{}";
            var depth = random.Next(1, 13);
            for (var index = 0; index < depth; index++)
            {
                value = random.Next(2) == 0 ? "[" + value + "]" : "{\"nested\":" + value + "}";
            }

            var payload = "{\"parameter_0\":" + value + "}";
            Assert.AreEqual(CommandResultKind.KeepOpen, page.ParameterForm.SubmitForm(payload, """{"action":"run"}""").Kind);
            var truncated = payload[..random.Next(payload.Length)];
            Assert.AreEqual(CommandResultKind.KeepOpen, page.ParameterForm.SubmitForm(truncated, """{"action":"run"}""").Kind);
            Assert.IsNull(session.Completion, $"Invalid corpus entry {iteration} started a script.");
        }

        Assert.AreEqual(0, service.RunCalls);
    }
}
