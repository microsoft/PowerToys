// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ManagedCommon
{
    public sealed class RunnerActionInvokeResult
    {
        public bool Success { get; set; }

        public string ErrorCode { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
