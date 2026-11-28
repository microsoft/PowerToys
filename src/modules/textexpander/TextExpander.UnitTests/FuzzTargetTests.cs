// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class FuzzTargetTests
{
    [TestMethod]
    [DataRow("{\"snippets\":[{}]}")]
    [DataRow("{\"snippets\":[{\"trigger\":\"\",\"replacement\":\"x\"}]}")]
    [DataRow("{\"snippets\":[{\"trigger\":null}]}")]
    [DataRow("{\"snippets\":[{}, {\"trigger\":\"a\",\"replacement\":\"one\"}, {\"trigger\":\"ba\",\"replacement\":\"two\"}]}")]
    public void SnippetFileFuzzingAcceptsIncompleteTriggers(string json)
        => global::TextExpander.FuzzTests.FuzzTests.FuzzSnippetFile(Encoding.UTF8.GetBytes(json));
}
