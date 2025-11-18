// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesEditor.Utils
{
    public readonly struct ParsingResult(bool result, string message = "", string data = "")
    {
        public bool Result { get; } = result;

        public string Message { get; } = message;

        public string MalformedData { get; } = data;
    }
}
