// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class SocketMappingTests
{
    private static readonly string[] SingleMapping = ["PEER 192.0.2.1"];
    private static readonly string[] CombinedMappings = ["PEER 192.0.2.1", "POLICY 192.0.2.2", "PEER 192.0.2.3", "USER 192.0.2.4"];

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("\r\n")]
    public void FirstUserMappingRemainsIntactWithoutPolicyMappings(string? policyMappings)
    {
        var mappings = SocketStuff.GetName2IpMappingLines(policyMappings!, "PEER 192.0.2.1");

        CollectionAssert.AreEqual(SingleMapping, mappings);
    }

    [DataTestMethod]
    [DataRow("", "")]
    [DataRow("\r\n", "")]
    [DataRow("", "\r\n")]
    [DataRow("\r\n", "\r\n")]
    public void MappingListsPreserveSeparateLinesAndPolicyOrder(string policySuffix, string userPrefix)
    {
        var mappings = SocketStuff.GetName2IpMappingLines(
            "PEER 192.0.2.1\r\nPOLICY 192.0.2.2" + policySuffix,
            userPrefix + "PEER 192.0.2.3\r\nUSER 192.0.2.4");

        CollectionAssert.AreEqual(CombinedMappings, mappings);
    }

    [TestMethod]
    public void EmptyMappingListsProduceNoRules()
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), SocketStuff.GetName2IpMappingLines(string.Empty, string.Empty));
    }
}
