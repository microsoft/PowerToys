// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class TransformHelpersTests
{
    [DataTestMethod]
    [DataRow(PasteFormats.LowerCase, "hello world")]
    [DataRow(PasteFormats.UpperCase, "HELLO WORLD")]
    [DataRow(PasteFormats.TitleCase, "Hello World")]
    [DataRow(PasteFormats.SentenceCase, "Hello world")]
    [DataRow(PasteFormats.ToggleCase, "HELLO world")]
    [DataRow(PasteFormats.CamelCase, "helloWorld")]
    [DataRow(PasteFormats.PascalCase, "HelloWorld")]
    [DataRow(PasteFormats.SnakeCase, "hello_world")]
    [DataRow(PasteFormats.ScreamingSnakeCase, "HELLO_WORLD")]
    [DataRow(PasteFormats.KebabCase, "hello-world")]
    public async Task TextCaseFormatsDispatchToExpectedTransformation(PasteFormats format, string expectedText)
    {
        var input = new DataPackage();
        input.SetText("hello WORLD");

        var output = await TransformHelpers.TransformAsync(format, input.GetView(), CancellationToken.None, progress: null);

        Assert.AreEqual(expectedText, await output.GetView().GetTextAsync().AsTask());
        Assert.IsFalse(PasteFormat.MetadataDict[format].RequiresAIService);
        CollectionAssert.AreEquivalent(new[] { StandardDataFormats.Text }, output.GetView().AvailableFormats.ToArray());
    }
}
