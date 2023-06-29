// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseWithoutBorders.Exceptions
{
    internal class KnownException : Exception
    {
        internal KnownException(string message)
            : base(message)
        {
        }
    }
}
