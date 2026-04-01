// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.ModuleContracts;

/// <summary>
/// Lightweight result type for module operations.
/// </summary>
public readonly record struct OperationResult(bool Success, string? Error = null)
{
    public static OperationResult Ok() => new(true, null);

    public static OperationResult Fail(string error) => new(false, error);
}

/// <summary>
/// Result type with a payload.
/// </summary>
public readonly record struct OperationResult<T>(bool Success, T? Value, string? Error = null);

/// <summary>
/// Factory helpers for creating operation results.
/// </summary>
public static class OperationResults
{
    public static OperationResult<T> Ok<T>(T value) => new(true, value, null);

    public static OperationResult<T> Fail<T>(string error) => new(false, default, error);
}
