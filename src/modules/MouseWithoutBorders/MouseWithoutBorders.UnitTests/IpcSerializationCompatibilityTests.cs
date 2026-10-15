// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using Newtonsoft.Json;
using StreamJsonRpc;

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
    public void ConnectRejectsMissingMachineNameBeforeChangingSettings(string? machineName)
    {
        var helperType = typeof(Program).GetNestedType("SettingsSyncHelper", BindingFlags.NonPublic)!;
        var helper = Activator.CreateInstance(
            helperType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { false },
            culture: System.Globalization.CultureInfo.InvariantCulture)!;
        var connect = helperType.GetMethod("ConnectToMachine")!;
        var exception = Assert.ThrowsException<TargetInvocationException>(() =>
            connect.Invoke(helper, new object?[] { machineName, "TEST-KEY" }));
        if (machineName == null)
        {
            Assert.IsInstanceOfType<ArgumentNullException>(exception.InnerException);
        }
        else
        {
            Assert.IsInstanceOfType<ArgumentException>(exception.InnerException);
        }
    }

    [TestMethod]
    public async Task SynchronousRequestValidationRemainsAnAwaitableRpcError()
    {
        var helperType = typeof(Program).GetNestedType("SettingsSyncHelper", BindingFlags.NonPublic)!;
        var helper = Activator.CreateInstance(
            helperType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { false },
            culture: System.Globalization.CultureInfo.InvariantCulture)!;
        var pipeName = $"PowerToys.MWB.RpcError.UnitTest.{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var connection = server.WaitForConnectionAsync();
        await client.ConnectAsync(5000);
        await connection.WaitAsync(TimeSpan.FromSeconds(5));

        using var serverRpc = JsonRpc.Attach(server, helper);
        using var clientRpc = JsonRpc.Attach(client);
        await Assert.ThrowsExceptionAsync<RemoteInvocationException>(() =>
            clientRpc.InvokeAsync("ConnectToMachine", string.Empty, "TEST-KEY").WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
