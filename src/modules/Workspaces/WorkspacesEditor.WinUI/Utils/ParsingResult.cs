// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesEditor.Utils
{
    public class ParsingResult
    {
        public bool Result { get; set; }

        public string Message { get; set; }

        public ParsingResult(bool result, string message = "")
        {
            Result = result;
            Message = message;
        }
    }
}
