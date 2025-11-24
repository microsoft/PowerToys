// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Monitor operation result
    /// </summary>
    public readonly struct MonitorOperationResult
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Error message
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// System error code
        /// </summary>
        public int? ErrorCode { get; }

        /// <summary>
        /// Operation timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        private MonitorOperationResult(bool isSuccess, string? errorMessage = null, int? errorCode = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static MonitorOperationResult Success() => new(true);

        /// <summary>
        /// Creates a failed result
        /// </summary>
        public static MonitorOperationResult Failure(string errorMessage, int? errorCode = null)
            => new(false, errorMessage, errorCode);

        public override string ToString()
        {
            return IsSuccess ? "Success" : $"Failed: {ErrorMessage}";
        }
    }
}
