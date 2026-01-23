// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Defines error codes for search operations.
/// </summary>
public enum SearchErrorCode
{
    /// <summary>
    /// No error occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// The search engine failed to initialize.
    /// </summary>
    InitializationFailed = 1,

    /// <summary>
    /// Failed to index content.
    /// </summary>
    IndexingFailed = 2,

    /// <summary>
    /// The search query failed to execute.
    /// </summary>
    SearchFailed = 3,

    /// <summary>
    /// The search engine is not ready to perform the operation.
    /// </summary>
    EngineNotReady = 4,

    /// <summary>
    /// A required capability is not available.
    /// </summary>
    CapabilityUnavailable = 5,

    /// <summary>
    /// The operation timed out.
    /// </summary>
    Timeout = 6,

    /// <summary>
    /// An unexpected error occurred.
    /// </summary>
    Unexpected = 99,
}
