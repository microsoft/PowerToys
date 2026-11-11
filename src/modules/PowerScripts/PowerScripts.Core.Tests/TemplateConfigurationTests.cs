// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Templates;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class TemplateConfigurationTests
{
    [TestMethod]
    public void ExistingConfiguredRequestsRetainTheirDefaults()
    {
        var options = TemplateConfiguration.Parse("""{"projectName":"Example","description":"An existing request."}""");
        Assert.AreEqual("powershell", options.Runtime);
        Assert.AreEqual("none", options.Input);
        Assert.AreEqual("text", options.Output);
        Assert.IsNull(options.Operation);
    }

    [TestMethod]
    public void OperationConfigurationRetainsTypedConstants()
    {
        var options = TemplateConfiguration.Parse("""
            {
              "projectName":"Inspect",
              "operation":{
                "commandId":"filelocksmith.query",
                "bindings":{"path":{"parameter":"path"},"json":{"constant":false}}
              }
            }
            """);
        Assert.AreEqual("filelocksmith.query", options.Operation!.CommandId);
        Assert.AreEqual("path", options.Operation.Bindings!["path"].Parameter);
        Assert.AreEqual(JsonValueKind.False, options.Operation.Bindings["json"].Constant.ValueKind);
    }

    [DataTestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("""{"projectName":"first","projectName":"second"}""")]
    [DataRow("""{"projectName":"Inspect","operation":{"commandId":"filelocksmith.query","bindings":{"json":{"constant":true,"constant":false}}}}""")]
    public void InvalidShapesAndDuplicatePropertiesAreRejected(string json) =>
        Assert.ThrowsException<InvalidDataException>(() => TemplateConfiguration.Parse(json));

    [DataTestMethod]
    [DataRow("""{"projectName":"Example","unrecognized":true}""")]
    [DataRow("""{"projectName":"Inspect","operation":{"commandId":"filelocksmith.query","executable":"other.exe"}}""")]
    [DataRow("""{"projectName":"Inspect","operation":{"commandId":"filelocksmith.query","bindings":{"path":{"parameter":"path","unrecognized":true}}}}""")]
    public void UnknownMembersCannotBeSilentlyIgnored(string json) =>
        Assert.ThrowsException<JsonException>(() => TemplateConfiguration.Parse(json));

    [TestMethod]
    public void OversizedConfigurationIsRejectedBeforeParsing() =>
        Assert.ThrowsException<InvalidDataException>(() => TemplateConfiguration.Parse(new string(' ', TemplateConfiguration.MaximumLength + 1)));
}
