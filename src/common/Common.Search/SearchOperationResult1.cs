// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable SA1649 // File name should match first type name - Generic type file naming convention

namespace Common.Search;

/// <summary>
/// Represents the result of a search operation that returns a value and may have errors.
/// </summary>
/// <typeparam name="T">The type of the result value.</typeparam>
public sealed class SearchOperationResult<T>
{
    private SearchOperationResult(bool success, T? value, SearchError? error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the result value if the operation was successful.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error if the operation failed, null otherwise.
    /// </summary>
    public SearchError? Error { get; }

    /// <summary>
    /// Gets the value or a default if the operation failed.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the operation failed.</param>
    /// <returns>The value if successful, otherwise the default value.</returns>
    public T GetValueOrDefault(T defaultValue) => IsSuccess && Value is not null ? Value : defaultValue;

    /// <inheritdoc/>
    public override string ToString()
        => IsSuccess ? $"Success: {Value}" : $"Failure: {Error}";

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The result value.</param>
    /// <returns>A successful SearchOperationResult.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory method pattern is the idiomatic way to create instances of generic result types")]
    public static SearchOperationResult<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <returns>A failed SearchOperationResult.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory method pattern is the idiomatic way to create instances of generic result types")]
    public static SearchOperationResult<T> Failure(SearchError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SearchOperationResult<T>(false, default, error);
    }

    /// <summary>
    /// Creates a failed result with the specified error and a fallback value.
    /// </summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <param name="fallbackValue">A fallback value to use despite the failure.</param>
    /// <returns>A failed SearchOperationResult with a fallback value.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory method pattern is the idiomatic way to create instances of generic result types")]
    public static SearchOperationResult<T> FailureWithFallback(SearchError error, T fallbackValue)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SearchOperationResult<T>(false, fallbackValue, error);
    }
}
