// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Carries the exception that caused a JSON-RPC transport error.
/// </summary>
public sealed class JsonRpcErrorEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcErrorEventArgs"/> class.
    /// </summary>
    /// <param name="exception">The exception describing the transport error.</param>
    public JsonRpcErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>
    /// Gets the exception describing the transport error.
    /// </summary>
    public Exception Exception { get; }
}
