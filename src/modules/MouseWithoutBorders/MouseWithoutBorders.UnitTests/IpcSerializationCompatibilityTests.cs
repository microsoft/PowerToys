// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
using Newtonsoft.Json;
using StreamJsonRpc;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
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
        var get = contract.GetMethod("GetCurrentPairingKey")!;

        Assert.AreEqual(typeof(Task<string>), get.ReturnType);
        Assert.AreEqual(0, get.GetParameters().Length);
        Assert.AreEqual(typeof(Task<string>), generate.ReturnType);
        Assert.AreEqual(0, generate.GetParameters().Length);
        Assert.AreEqual(typeof(Task), connect.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(string) },
            connect.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    [DataTestMethod]
    [DataRow(false, "CURRENT-RUNTIME-KEY")]
    [DataRow(true, "CURRENT-RUNTIME-KEY")]
    [DataRow(true, null)]
    [DataRow(true, "")]
    [DataRow(true, " \t\r\n")]
    public async Task GetCurrentPairingKeyReadsOnlyTheCurrentRuntimeKeyOverRpc(bool canManageServicePairing, string? runtimeKey)
    {
        if (Class.Settings.IsProtectedMode)
        {
            Assert.Inconclusive("This case requires a normal user token; service authorization is covered separately.");
        }

        var previousKey = Encryption.MyKey;
        var previousMagic = Encryption.MagicNumber;
        var previousGenerated = Encryption.GeneratedKey;
        var previousCorrupted = Encryption.KeyCorrupted;
        try
        {
            var helperType = typeof(Program).GetNestedType("SettingsSyncHelper", BindingFlags.NonPublic)!;
            var helper = Activator.CreateInstance(
                helperType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { canManageServicePairing },
                culture: System.Globalization.CultureInfo.InvariantCulture)!;
            var pipeName = $"PowerToys.MWB.CurrentKey.UnitTest.{Guid.NewGuid():N}";
            await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            var connection = server.WaitForConnectionAsync();
            await client.ConnectAsync(5000);
            await connection.WaitAsync(TimeSpan.FromSeconds(5));
            using var serverRpc = JsonRpc.Attach(server, helper);
            using var clientRpc = JsonRpc.Attach(client);
            Encryption.MyKey = runtimeKey!;
            if (string.IsNullOrWhiteSpace(runtimeKey))
            {
                var exception = await Assert.ThrowsExceptionAsync<RemoteInvocationException>(() =>
                    clientRpc.InvokeAsync<string>("GetCurrentPairingKey").WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.AreEqual("Pairing setup is required.", exception.Message);
            }
            else
            {
                Assert.AreEqual(runtimeKey, await clientRpc.InvokeAsync<string>("GetCurrentPairingKey").WaitAsync(TimeSpan.FromSeconds(5)));
                Encryption.MyKey = "REPLACED-RUNTIME-KEY";
                Assert.AreEqual(Encryption.MyKey, await clientRpc.InvokeAsync<string>("GetCurrentPairingKey").WaitAsync(TimeSpan.FromSeconds(5)));
                Encryption.MyKey = runtimeKey;
            }

            Assert.AreEqual(runtimeKey, Encryption.MyKey);
            Assert.AreEqual(previousMagic, Encryption.MagicNumber);
            Assert.AreEqual(previousGenerated, Encryption.GeneratedKey);
            Assert.AreEqual(previousCorrupted, Encryption.KeyCorrupted);
        }
        finally
        {
            Encryption.MyKey = previousKey;
            Encryption.MagicNumber = previousMagic;
            Encryption.GeneratedKey = previousGenerated;
            Encryption.KeyCorrupted = previousCorrupted;
        }
    }

    [TestMethod]
    public void GetCurrentPairingKeyAuthorizesBeforeReadingRuntimeStateWithoutAccessingStoredSettings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        const string relativePath = @"src\modules\MouseWithoutBorders\App\Class\Program.cs";
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Repository source must be available for the authorization wiring check.");
        var source = File.ReadAllText(Path.Combine(directory.FullName, relativePath));
        var start = source.IndexOf("public Task<string> GetCurrentPairingKey()", StringComparison.Ordinal);
        var end = source.IndexOf("public Task ConnectToMachine(", start, StringComparison.Ordinal);
        var method = source.Substring(start, end - start);
        var guard = method.IndexOf("EnsurePairingOperationAuthorized(Settings.IsProtectedMode, _canManageServicePairing);", StringComparison.Ordinal);
        var read = method.IndexOf("Encryption.MyKey", StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0 && read > guard);
        Assert.AreEqual(read, method.LastIndexOf("Encryption.MyKey", StringComparison.Ordinal));
        Assert.IsFalse(method.Contains("Setting.Values", StringComparison.Ordinal));
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
