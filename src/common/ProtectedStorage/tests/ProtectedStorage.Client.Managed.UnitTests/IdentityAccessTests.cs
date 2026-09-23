// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.ProtectedStorage;

namespace ProtectedStorage.Client.Managed.UnitTests;

[TestClass]
public sealed class IdentityAccessTests
{
    [TestMethod]
    public void VaIdentityGrantIncludesBothQueryBitsButNoMutationOrMemoryAccess()
    {
        var type = typeof(ProtectedStoreClient).Assembly.GetType("PowerToys.ProtectedStorage.ClientIdentity", throwOnError: true)!;
        int process = (int)type.GetField("ProcessIdentityAccess", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        int token = (int)type.GetField("TokenIdentityAccess", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        Assert.AreEqual(0x101400, process);
        Assert.AreEqual(0x0400, process & 0x0400);
        Assert.AreEqual(0x1000, process & 0x1000);
        Assert.AreEqual(0x100000, process & 0x100000);
        Assert.AreEqual(0, process & (0x0001 | 0x0002 | 0x0008 | 0x0010 | 0x0020 | 0x0040 | 0x0080 | 0x0100 | 0x0200 | 0x2000));
        Assert.AreEqual(0x0008, token);
    }
}
