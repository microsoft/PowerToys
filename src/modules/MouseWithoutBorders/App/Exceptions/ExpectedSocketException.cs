// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Sockets;

namespace MouseWithoutBorders.Exceptions
{
    internal class ExpectedSocketException : KnownException
    {
        internal bool ShouldReconnect { get; set; }

        internal ExpectedSocketException(string message)
            : base(message)
        {
        }

        internal ExpectedSocketException(SocketException se)
            : base(se.Message)
        {
            ShouldReconnect = se.ErrorCode == 10054;
        }
    }
}
