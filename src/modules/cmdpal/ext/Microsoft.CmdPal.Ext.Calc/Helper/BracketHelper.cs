// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static class BracketHelper
{
    public static bool IsBracketComplete(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var valueTuples = query
            .Select(BracketTrail)
            .Where(r => r != default);

        var trailTest = new Stack<TrailType>();

        foreach (var (direction, type) in valueTuples)
        {
            switch (direction)
            {
                case TrailDirection.Open:
                    trailTest.Push(type);
                    break;
                case TrailDirection.Close:
                    // Try to get item out of stack
                    if (!trailTest.TryPop(out var popped))
                    {
                        return false;
                    }

                    if (type != popped)
                    {
                        return false;
                    }

                    continue;
                default:
                    {
                        throw new ArgumentOutOfRangeException($"Can't process value (Parameter direction: {direction})");
                    }
            }
        }

        return trailTest.Count == 0;
    }

    public static string BalanceBrackets(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return query ?? string.Empty;
        }

        var openBrackets = new Stack<TrailType>();

        for (var i = 0; i < query.Length; i++)
        {
            var (direction, type) = BracketTrail(query[i]);

            if (direction == TrailDirection.None)
            {
                continue;
            }

            if (direction == TrailDirection.Open)
            {
                openBrackets.Push(type);
            }
            else if (direction == TrailDirection.Close)
            {
                // Only pop if we have a matching open bracket
                if (openBrackets.Count > 0 && openBrackets.Peek() == type)
                {
                    openBrackets.Pop();
                }
            }
        }

        if (openBrackets.Count == 0)
        {
            return query;
        }

        // Build closing brackets in LIFO order
        var closingBrackets = new char[openBrackets.Count];
        var index = 0;

        while (openBrackets.Count > 0)
        {
            var type = openBrackets.Pop();
            closingBrackets[index++] = type == TrailType.Round ? ')' : ']';
        }

        return query + new string(closingBrackets);
    }

    private static (TrailDirection Direction, TrailType Type) BracketTrail(char @char)
    {
        switch (@char)
        {
            case '(':
                return (TrailDirection.Open, TrailType.Round);
            case ')':
                return (TrailDirection.Close, TrailType.Round);
            case '[':
                return (TrailDirection.Open, TrailType.Bracket);
            case ']':
                return (TrailDirection.Close, TrailType.Bracket);
            default:
                return default;
        }
    }

    private enum TrailDirection
    {
        None,
        Open,
        Close,
    }

    private enum TrailType
    {
        None,
        Bracket,
        Round,
    }
}
