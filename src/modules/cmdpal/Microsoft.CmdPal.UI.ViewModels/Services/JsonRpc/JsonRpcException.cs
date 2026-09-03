// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Thrown when a JSON-RPC request completes with an error response.
/// </summary>
public sealed class JsonRpcException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcException"/> class.
    /// </summary>
    public JsonRpcException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JsonRpcException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public JsonRpcException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcException"/> class from a JSON-RPC error object.
    /// </summary>
    /// <param name="error">The error object returned by the remote peer.</param>
    public JsonRpcException(JsonRpcError error)
        : base(error?.Message ?? string.Empty)
    {
        Code = error?.Code ?? JsonRpcError.InternalError;
    }

    /// <summary>
    /// Gets the numeric JSON-RPC error code associated with this failure.
    /// </summary>
    public int Code { get; }
}
