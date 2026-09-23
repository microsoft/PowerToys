// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.ProtectedStorage;

public sealed class ProtectedStorageException : IOException
{
    public ProtectedStorageException(string errorCode, Guid operationId = default, int nativeCode = 0, Exception? innerException = null)
        : base($"Protected storage: {errorCode}.", innerException)
    {
        ErrorCode = errorCode;
        OperationId = operationId;
        NativeCode = nativeCode;
    }

    public string ErrorCode { get; }

    public Guid OperationId { get; }

    public int NativeCode { get; }
}
