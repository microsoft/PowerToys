// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record CommandAlias
{
    public string CommandId { get; init; }

    public string Alias { get; init; }

    public bool IsDirect { get; init; }

    [JsonIgnore]
    public string SearchPrefix => Alias + (IsDirect ? string.Empty : " ");

    public CommandAlias(string shortcut, string commandId, bool direct = false)
    {
        CommandId = commandId;
        Alias = shortcut;
        IsDirect = direct;
    }

    public CommandAlias()
        : this(string.Empty, string.Empty, false)
    {
    }

    public static CommandAlias FromSearchText(string text, string commandId)
    {
        var trailingSpace = text.EndsWith(' ');
        var realAlias = trailingSpace ? text.Substring(0, text.Length - 1) : text;
        return new CommandAlias(realAlias, commandId, !trailingSpace);
    }
}
