// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class JsonRpcMessageTests
{
    // NOTE: These tests are scaffolding for the JsonRpc message types that Parker is building.
    // Once the implementation is available, update the type references accordingly.
    [TestMethod]
    public void JsonRpcRequest_Serialize_ProducesCorrectFormat()
    {
        // Arrange
        // TODO: Replace with actual JsonRpcRequest type once implemented
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "testMethod",
            @params = new { arg1 = "value1", arg2 = 42 },
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        Assert.IsNotNull(json);
        StringAssert.Contains(json, "\"jsonrpc\":\"2.0\"");
        StringAssert.Contains(json, "\"id\":1");
        StringAssert.Contains(json, "\"method\":\"testMethod\"");
        StringAssert.Contains(json, "\"params\"");
    }

    [TestMethod]
    public void JsonRpcRequest_Deserialize_ReturnsCorrectValues()
    {
        // Arrange
        var json = """
            {
              "jsonrpc": "2.0",
              "id": 1,
              "method": "testMethod",
              "params": {
                "arg1": "value1",
                "arg2": 42
              }
            }
            """;

        // Act
        // TODO: Replace with actual JsonRpcRequest type once implemented
        var request = JsonSerializer.Deserialize<dynamic>(json);

        // Assert
        Assert.IsNotNull(request);

        // Additional assertions will be added once types are available
    }

    [TestMethod]
    public void JsonRpcResponse_Serialize_WithResult_ProducesCorrectFormat()
    {
        // Arrange
        // TODO: Replace with actual JsonRpcResponse type once implemented
        var response = new
        {
            jsonrpc = "2.0",
            id = 1,
            result = new { data = "success" },
        };

        // Act
        var json = JsonSerializer.Serialize(response);

        // Assert
        Assert.IsNotNull(json);
        StringAssert.Contains(json, "\"jsonrpc\":\"2.0\"");
        StringAssert.Contains(json, "\"id\":1");
        StringAssert.Contains(json, "\"result\"");
    }

    [TestMethod]
    public void JsonRpcResponse_Serialize_WithError_ProducesCorrectFormat()
    {
        // Arrange
        // TODO: Replace with actual JsonRpcResponse and JsonRpcError types once implemented
        var response = new
        {
            jsonrpc = "2.0",
            id = 1,
            error = new
            {
                code = -32600,
                message = "Invalid Request",
                data = (string?)null,
            },
        };

        // Act
        var json = JsonSerializer.Serialize(response);

        // Assert
        Assert.IsNotNull(json);
        StringAssert.Contains(json, "\"jsonrpc\":\"2.0\"");
        StringAssert.Contains(json, "\"id\":1");
        StringAssert.Contains(json, "\"error\"");
        StringAssert.Contains(json, "\"code\":-32600");
        StringAssert.Contains(json, "\"message\":\"Invalid Request\"");
    }

    [TestMethod]
    public void JsonRpcNotification_Serialize_NoId_ProducesCorrectFormat()
    {
        // Arrange
        // TODO: Replace with actual JsonRpcNotification type once implemented
        var notification = new
        {
            jsonrpc = "2.0",
            method = "notifyMethod",
            @params = new { message = "notification" },
        };

        // Act
        var json = JsonSerializer.Serialize(notification);

        // Assert
        Assert.IsNotNull(json);
        StringAssert.Contains(json, "\"jsonrpc\":\"2.0\"");
        StringAssert.Contains(json, "\"method\":\"notifyMethod\"");
        Assert.IsFalse(json.Contains("\"id\""), "Notifications should not have an id field");
    }

    [TestMethod]
    public void JsonRpcError_StandardErrorCodes_AreCorrect()
    {
        // Standard JSON-RPC error codes
        // TODO: Once error code constants are defined, reference them here
        var parseError = -32700;
        var invalidRequest = -32600;
        var methodNotFound = -32601;
        var invalidParams = -32602;
        var internalError = -32603;

        // Assert standard codes are as expected
        Assert.AreEqual(-32700, parseError);
        Assert.AreEqual(-32600, invalidRequest);
        Assert.AreEqual(-32601, methodNotFound);
        Assert.AreEqual(-32602, invalidParams);
        Assert.AreEqual(-32603, internalError);
    }

    [TestMethod]
    public void JsonRpcRequest_WithNullParams_SerializesCorrectly()
    {
        // Arrange
        // TODO: Replace with actual JsonRpcRequest type once implemented
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "testMethod",
            @params = (object?)null,
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        Assert.IsNotNull(json);
        StringAssert.Contains(json, "\"jsonrpc\":\"2.0\"");
        StringAssert.Contains(json, "\"method\":\"testMethod\"");
    }

    [TestMethod]
    public void JsonRpcResponse_Deserialize_WithResult_ReturnsCorrectValues()
    {
        // Arrange
        var json = """
            {
              "jsonrpc": "2.0",
              "id": 1,
              "result": {
                "data": "success",
                "count": 42
              }
            }
            """;

        // Act
        // TODO: Replace with actual JsonRpcResponse type once implemented
        var response = JsonSerializer.Deserialize<dynamic>(json);

        // Assert
        Assert.IsNotNull(response);

        // Additional assertions will be added once types are available
    }

    [TestMethod]
    public void JsonRpcResponse_Deserialize_WithError_ReturnsCorrectValues()
    {
        // Arrange
        var json = """
            {
              "jsonrpc": "2.0",
              "id": 1,
              "error": {
                "code": -32601,
                "message": "Method not found",
                "data": "Additional error info"
              }
            }
            """;

        // Act
        // TODO: Replace with actual JsonRpcResponse type once implemented
        var response = JsonSerializer.Deserialize<dynamic>(json);

        // Assert
        Assert.IsNotNull(response);

        // Additional assertions will be added once types are available
    }

    [TestMethod]
    public void JsonRpcRequest_RoundTrip_PreservesData()
    {
        // Arrange
        // TODO: Replace with actual JsonRpcRequest type once implemented
        var originalRequest = new
        {
            jsonrpc = "2.0",
            id = 123,
            method = "calculateSum",
            @params = new { numbers = new[] { 1, 2, 3, 4, 5 } },
        };

        // Act
        var json = JsonSerializer.Serialize(originalRequest);
        var deserializedRequest = JsonSerializer.Deserialize<dynamic>(json);

        // Assert
        Assert.IsNotNull(json);
        Assert.IsNotNull(deserializedRequest);

        // Full round-trip validation will be added once types are available
    }
}
