// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.CmdPal.Common.Helpers;

public static class ShellArgumentBuilder
{
    public static string BuildArguments(params string[] arguments)
    {
        if (arguments.Length <= 0)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();
        foreach (var argument in arguments)
        {
            AppendArgument(stringBuilder, argument);
        }

        return stringBuilder.ToString();
    }

    private static void AppendArgument(StringBuilder stringBuilder, string argument)
    {
        if (stringBuilder.Length > 0)
        {
            stringBuilder.Append(' ');
        }

        if (argument.Length == 0 || ShouldBeQuoted(argument))
        {
            stringBuilder.Append('"');
            var index = 0;
            while (index < argument.Length)
            {
                var c = argument[index++];
                if (c == '\\')
                {
                    var numBackSlash = 1;
                    while (index < argument.Length && argument[index] == '\\')
                    {
                        index++;
                        numBackSlash++;
                    }

                    if (index == argument.Length)
                    {
                        stringBuilder.Append('\\', numBackSlash * 2);
                    }
                    else if (argument[index] == '"')
                    {
                        stringBuilder.Append('\\', (numBackSlash * 2) + 1);
                        stringBuilder.Append('"');
                        index++;
                    }
                    else
                    {
                        stringBuilder.Append('\\', numBackSlash);
                    }

                    continue;
                }

                if (c == '"')
                {
                    stringBuilder.Append('\\');
                    stringBuilder.Append('"');
                    continue;
                }

                stringBuilder.Append(c);
            }

            stringBuilder.Append('"');
        }
        else
        {
            stringBuilder.Append(argument);
        }
    }

    private static bool ShouldBeQuoted(string argument)
    {
        foreach (var c in argument)
        {
            if (char.IsWhiteSpace(c) || c == '"')
            {
                return true;
            }
        }

        return false;
    }
}
