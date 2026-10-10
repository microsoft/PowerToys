// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class SocketStuffTests
{
    [TestMethod]
    public void GetMappedAddressesShouldUseASingleRule()
    {
        var addresses = SocketStuff.GetMappedAddresses(string.Empty, "secdk-gm19dvh3 192.168.0.221", "SECDK-GM19DVH3");

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.168.0.221") }, addresses);
    }

    [TestMethod]
    public void GetMappedAddressesShouldUseTheFirstOfSeveralRules()
    {
        var addresses = SocketStuff.GetMappedAddresses(string.Empty, "PC1 10.0.0.1\r\nPC2 10.0.0.2", "PC1");

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("10.0.0.1") }, addresses);
    }

    [TestMethod]
    public void GetMappedAddressesShouldUsePolicyRulesWithoutUserRules()
    {
        var addresses = SocketStuff.GetMappedAddresses("PC1 10.0.0.1", string.Empty, "PC1");

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("10.0.0.1") }, addresses);
    }

    [TestMethod]
    public void GetMappedAddressesShouldUsePolicyAndUserRulesAndListEachAddressOnce()
    {
        var addresses = SocketStuff.GetMappedAddresses("PC1 10.0.0.1", "PC1 10.0.0.1\r\nPC1 10.0.0.2", "PC1");

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.2") }, addresses);
    }

    [TestMethod]
    public void GetMappedAddressesShouldIgnoreRulesForOtherMachinesAndRulesWithoutAValidAddress()
    {
        // "placeholder 0.0.0.0" is the line users add as a workaround for the first rule being ignored.
        var addresses = SocketStuff.GetMappedAddresses(string.Empty, "placeholder 0.0.0.0\r\nPC2 10.0.0.2\r\nPC1 not-an-address\r\nPC1", "PC1");

        Assert.AreEqual(0, addresses.Count);
    }
}
