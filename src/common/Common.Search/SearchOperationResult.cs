// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Represents the result of a search operation that may have errors.
/// </summary>
public sealed class SearchOperationResult
{
    private SearchOperationResult(bool success, SearchError? error = null)
    {
        IsSuccess = success;
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
    /// Gets the error if the operation failed, null otherwise.
    /// </summary>
    public SearchError? Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful SearchOperationResult.</returns>
    public static SearchOperationResult Success() => new(true);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <returns>A failed SearchOperationResult.</returns>
    public static SearchOperationResult Failure(SearchError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SearchOperationResult(false, error);
    }

    /// <inheritdoc/>
    public override string ToString()
        => IsSuccess ? "Success" : $"Failure: {Error}";
}
