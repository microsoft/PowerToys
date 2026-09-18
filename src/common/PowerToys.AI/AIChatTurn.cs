// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.AI;

/// <summary>
/// One turn in a chat completion conversation.
/// </summary>
public sealed class AIChatTurn
{
    public AIChatTurn(string text, bool isUser)
    {
        Text = text ?? string.Empty;
        IsUser = isUser;
    }

    public string Text { get; }

    public bool IsUser { get; }

    public static AIChatTurn User(string text) => new(text, isUser: true);

    public static AIChatTurn Assistant(string text) => new(text, isUser: false);
}
