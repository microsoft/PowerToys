// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
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
                            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Can't process value");
                        }
                }
            }

            return !trailTest.Any();
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
}
