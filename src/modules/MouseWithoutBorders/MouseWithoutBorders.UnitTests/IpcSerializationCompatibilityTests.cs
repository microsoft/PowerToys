// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Threading.Tasks;

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

    [TestMethod]
    public void PairingRpcContractUsesAwaitableResponsesWithoutCallerFlags()
    {
        var contract = typeof(Program).GetNestedType("ISettingsSyncHelper", BindingFlags.NonPublic)!;
        var generate = contract.GetMethod("GenerateNewKey")!;
        var connect = contract.GetMethod("ConnectToMachine")!;

        Assert.AreEqual(typeof(Task<string>), generate.ReturnType);
        Assert.AreEqual(0, generate.GetParameters().Length);
        Assert.AreEqual(typeof(Task), connect.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            connect.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task ConnectRejectsMissingMachineNameBeforeChangingSettings(string? machineName)
    {
        var helperType = typeof(Program).GetNestedType("SettingsSyncHelper", BindingFlags.NonPublic)!;
        var settings = (MouseWithoutBorders.Class.Settings)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MouseWithoutBorders.Class.Settings));
        var helper = Activator.CreateInstance(
            helperType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { new SettingsSyncAuthorization(false), settings },
            culture: System.Globalization.CultureInfo.InvariantCulture)!;
        var connect = helperType.GetMethod("ConnectToMachine")!;
        var request = (Task)connect.Invoke(helper, new object?[] { machineName, "TEST-KEY" })!;
        if (machineName == null)
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => request);
        }
        else
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => request);
        }
    }
}
