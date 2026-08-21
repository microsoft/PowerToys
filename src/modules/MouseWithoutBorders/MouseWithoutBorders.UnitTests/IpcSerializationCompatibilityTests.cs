// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using Newtonsoft.Json;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class IpcSerializationCompatibilityTests
{
    [TestMethod]
    public void SettingsSyncPayloadKeepsExistingJsonShape()
    {
        var contract = typeof(Program).GetNestedType("ISettingsSyncHelper", BindingFlags.NonPublic);
        var stateType = contract!.GetNestedType("MachineSocketState");
        var state = Activator.CreateInstance(stateType!);
        stateType!.GetField("Name")!.SetValue(state, "PC");
        stateType.GetField("Status")!.SetValue(state, Enum.ToObject(stateType.GetField("Status")!.FieldType, 9));

        Assert.AreEqual("""{"Name":"PC","Status":9}""", JsonConvert.SerializeObject(state));
    }
}
