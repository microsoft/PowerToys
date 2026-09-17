// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Raised when a command could not be generated. The message is safe to show to the user.
    /// </summary>
    public sealed class AIGenerationException : Exception
    {
        public AIGenerationException()
        {
        }

        public AIGenerationException(string message)
            : base(message)
        {
        }

        public AIGenerationException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
