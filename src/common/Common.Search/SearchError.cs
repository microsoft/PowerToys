// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Represents an error that occurred during a search operation.
/// </summary>
public sealed class SearchError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchError"/> class.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="details">Optional additional details.</param>
    /// <param name="exception">Optional exception that caused the error.</param>
    public SearchError(SearchErrorCode code, string message, string? details = null, Exception? exception = null)
    {
        Code = code;
        Message = message;
        Details = details;
        Exception = exception;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public SearchErrorCode Code { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets additional details about the error.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Gets the exception that caused the error, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates an error for initialization failure.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <param name="details">Optional details.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new SearchError instance.</returns>
    public static SearchError InitializationFailed(string indexName, string? details = null, Exception? exception = null)
        => new(SearchErrorCode.InitializationFailed, $"Failed to initialize search index '{indexName}'.", details, exception);

    /// <summary>
    /// Creates an error for indexing failure.
    /// </summary>
    /// <param name="contentId">The ID of the content that failed to index.</param>
    /// <param name="details">Optional details.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new SearchError instance.</returns>
    public static SearchError IndexingFailed(string contentId, string? details = null, Exception? exception = null)
        => new(SearchErrorCode.IndexingFailed, $"Failed to index content '{contentId}'.", details, exception);

    /// <summary>
    /// Creates an error for search query failure.
    /// </summary>
    /// <param name="query">The search query that failed.</param>
    /// <param name="details">Optional details.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new SearchError instance.</returns>
    public static SearchError SearchFailed(string query, string? details = null, Exception? exception = null)
        => new(SearchErrorCode.SearchFailed, $"Search query '{query}' failed.", details, exception);

    /// <summary>
    /// Creates an error for engine not ready.
    /// </summary>
    /// <param name="operation">The operation that was attempted.</param>
    /// <returns>A new SearchError instance.</returns>
    public static SearchError EngineNotReady(string operation)
        => new(SearchErrorCode.EngineNotReady, $"Search engine is not ready. Operation '{operation}' cannot be performed.");

    /// <summary>
    /// Creates an error for capability unavailable.
    /// </summary>
    /// <param name="capability">The capability that is unavailable.</param>
    /// <param name="details">Optional details.</param>
    /// <returns>A new SearchError instance.</returns>
    public static SearchError CapabilityUnavailable(string capability, string? details = null)
        => new(SearchErrorCode.CapabilityUnavailable, $"Search capability '{capability}' is not available.", details);

    /// <summary>
    /// Creates an error for timeout.
    /// </summary>
    /// <param name="operation">The operation that timed out.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>A new SearchError instance.</returns>
    public static SearchError Timeout(string operation, TimeSpan timeout)
        => new(SearchErrorCode.Timeout, $"Operation '{operation}' timed out after {timeout.TotalSeconds:F1} seconds.");

    /// <summary>
    /// Creates an error for an unexpected error.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A new SearchError instance.</returns>
    public static SearchError Unexpected(string operation, Exception exception)
        => new(SearchErrorCode.Unexpected, $"Unexpected error during '{operation}'.", exception.Message, exception);

    /// <inheritdoc/>
    public override string ToString()
    {
        var result = $"[{Code}] {Message}";
        if (!string.IsNullOrEmpty(Details))
        {
            result += $" Details: {Details}";
        }

        if (Exception != null)
        {
            result += $" Exception: {Exception.GetType().Name}: {Exception.Message}";
        }

        return result;
    }
}
