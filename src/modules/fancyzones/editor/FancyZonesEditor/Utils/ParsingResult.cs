// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor.Utils
{
    public struct ParsingResult
    {
        public bool Result { get; }

        public string Message { get; }

        public string MalformedData { get; }

        public ParsingResult(bool result, string message = "", string data = "")
        {
            Result = result;
            Message = message;
            MalformedData = data;
        }
    }
}
