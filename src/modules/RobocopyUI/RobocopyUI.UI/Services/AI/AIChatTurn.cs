// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// A single message in the command generation conversation.
    /// </summary>
    /// <param name="IsUser">True when the message came from the user, false when it came from the model.</param>
    /// <param name="Text">The message content.</param>
    public record AIChatTurn(bool IsUser, string Text);
}
